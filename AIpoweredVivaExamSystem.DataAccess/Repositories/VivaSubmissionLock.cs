using System.Data;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

internal static class VivaSubmissionLock
{
    // A session lock spans speech/AI without holding a transaction or blocking another student.
    public static async Task<IAsyncDisposable> AcquireAsync(ApplicationDbContext db, Guid sessionId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var lease = new Lease(db, Resource(sessionId));
        try
        {
            var result = await GetAsync(db, lease.Resource, "Session", 30000, ct);
            if (result < 0)
                throw new ResourceConflictException("Câu trả lời đang được xử lý. Hãy đợi rồi tải lại phiên thi.");
            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }

    // Called inside the exam transaction. Never wait: a submit owns the session lock first
    // and will briefly acquire the exam lock to commit. Waiting here would reverse that order.
    public static async Task<bool> TryProtectExpiryAsync(ApplicationDbContext db, Guid sessionId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Expiry requires the exam transaction.");
        var result = await GetAsync(db, Resource(sessionId), "Transaction", 0, ct);
        if (result == -1) return false;
        if (result < 0) throw new ResourceConflictException("Không thể kết thúc phiên thi lúc này. Hãy thử lại.");
        return true;
    }

    private static string Resource(Guid sessionId) => $"VivaSubmission:{sessionId:D}";
    private static async Task<int> GetAsync(ApplicationDbContext db, string resource, string owner, int timeout, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandTimeout = 35;
        command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner=@owner, @LockTimeout=@timeout; SELECT @result;";
        command.Parameters.Add(new SqlParameter("@resource", resource));
        command.Parameters.Add(new SqlParameter("@owner", owner));
        command.Parameters.Add(new SqlParameter("@timeout", timeout));
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private sealed class Lease(ApplicationDbContext db, string resource) : IAsyncDisposable
    {
        public string Resource { get; } = resource;
        public async ValueTask DisposeAsync()
        {
            try
            {
                var connection = db.Database.GetDbConnection();
                if (connection.State == ConnectionState.Open)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
                    command.Parameters.Add(new SqlParameter("@resource", Resource));
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            catch
            {
                // Do not return a connection with an unknown session-lock state to the pool.
                if (db.Database.GetDbConnection() is SqlConnection sql) SqlConnection.ClearPool(sql);
                throw;
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
