internal sealed record DemoCriterion(string Name,string Concepts,decimal Max);
internal sealed record DemoQuestion(string Key,string Topic,string Text,string Expected,DemoCriterion[] Criteria);
internal static class DemoContent
{
    public static readonly DemoQuestion[] Questions = [
        new("di","Dependency Injection",
            "Dependency Injection trong ASP.NET Core là gì? Phân biệt Singleton, Scoped và Transient, kèm ví dụ.",
            "Dependency được cung cấp từ bên ngoài qua constructor; DI container tạo và quản lý đối tượng. Singleton dùng một instance trong ứng dụng; Scoped một instance mỗi scope/request; Transient tạo mới mỗi lần resolve. DbContext thường Scoped; không giữ Scoped trong Singleton.",
            [new("Giải thích DI và container","constructor injection, inversion of control, DI container",4),new("Phân biệt ba lifetime","Singleton toàn ứng dụng; Scoped mỗi request/scope; Transient mỗi resolve",4),new("Ví dụ và lưu ý","DbContext Scoped, captive dependency, không inject Scoped vào Singleton",2)]),
        new("ef","Entity Framework Core",
            "EF Core theo dõi thay đổi của entity như thế nào? Khi nào nên dùng AsNoTracking?",
            "DbContext dùng ChangeTracker lưu entity và trạng thái Added, Modified, Deleted, Unchanged. DetectChanges so giá trị hiện tại với snapshot; SaveChanges phát sinh INSERT, UPDATE, DELETE. AsNoTracking phù hợp truy vấn chỉ đọc, giảm chi phí tracking; muốn cập nhật cần attach hoặc truy vấn tracking.",
            [new("Cơ chế tracking","ChangeTracker, EntityState, snapshot, DetectChanges",4),new("Ghi thay đổi","SaveChanges sinh INSERT UPDATE DELETE theo trạng thái entity",3),new("AsNoTracking","truy vấn chỉ đọc, giảm bộ nhớ và chi phí, attach khi cập nhật",3)]),
        new("mvc","ASP.NET Core",
            "Trình bày luồng xử lý một yêu cầu HTTP theo mô hình MVC trong ASP.NET Core.",
            "Routing chọn controller/action; model binding đọc dữ liệu request và validation kiểm tra đầu vào. Controller gọi service xử lý nghiệp vụ và lấy model, rồi trả view hoặc kết quả HTTP. View hiển thị, model mang dữ liệu/quy tắc, controller điều phối. Tách trách nhiệm hỗ trợ kiểm thử và bảo trì.",
            [new("Vai trò MVC","Model dữ liệu/nghiệp vụ, View hiển thị, Controller điều phối",4),new("Luồng request","routing, model binding, validation, service, action result",4),new("Lợi ích và ví dụ","tách trách nhiệm, kiểm thử, ví dụ controller gọi service",2)]),
        new("async","ASP.NET Core",
            "Vì sao dùng async/await khi truy vấn cơ sở dữ liệu trong ASP.NET Core? Có nên bọc mọi truy vấn trong Task.Run không?",
            "I/O bất đồng bộ cho phép trả thread về pool trong lúc chờ SQL, giúp phục vụ nhiều request. await tiếp tục khi tác vụ hoàn thành; không tự làm truy vấn nhanh hơn hay tạo thread mới. Dùng ToListAsync/SaveChangesAsync, truyền CancellationToken; tránh .Result/.Wait và Task.Run quanh I/O vốn có API async.",
            [new("Hiểu I/O bất đồng bộ","giải phóng thread khi chờ I/O, khả năng phục vụ đồng thời",4),new("Cách dùng EF async","await ToListAsync hoặc SaveChangesAsync, CancellationToken",3),new("Tránh blocking và Task.Run","không .Result/.Wait, không bọc I/O async bằng Task.Run",3)]),
        new("middleware","ASP.NET Core",
            "Middleware trong ASP.NET Core hoạt động thế nào? Vì sao thứ tự xác thực và phân quyền quan trọng?",
            "Các middleware tạo pipeline, xử lý request trước next và response khi quay ngược lại; có thể short-circuit. Exception handler đặt sớm để bắt lỗi phía sau. Routing chọn endpoint, Authentication xác định danh tính, Authorization kiểm tra quyền dựa trên danh tính rồi thực thi endpoint; đảo thứ tự có thể không áp dụng đúng quyền.",
            [new("Pipeline","RequestDelegate next, request đi tới response đi ngược, short-circuit",4),new("Thứ tự xử lý","exception handler sớm, routing trước endpoint",3),new("Xác thực và phân quyền","Authentication xác định danh tính trước Authorization kiểm tra quyền",3)]),
        new("csrf","ASP.NET Core",
            "Antiforgery token bảo vệ Razor Pages khỏi tấn công nào và được kiểm tra như thế nào?",
            "Token chống CSRF: trình duyệt có thể tự gửi cookie đăng nhập trong một yêu cầu giả mạo từ website khác. Server sinh cookie token và request token trong hidden field/header, xác thực cặp token khi POST. Form tag helper tạo token và Razor Pages tự kiểm tra handler POST; thiếu/sai trả 400. Token không thay thế kiểm tra quyền.",
            [new("Mục đích","CSRF, trình duyệt tự gửi cookie xác thực",3),new("Cặp token","cookie token và request token hidden field hoặc header, server xác thực",4),new("Razor Pages","form tag helper, POST tự kiểm tra, 400 khi thiếu/sai, vẫn cần phân quyền",3)])
    ];
    public const string StrongDi="Dạ, Dependency Injection là đưa dependency từ bên ngoài vào lớp, thường qua constructor thay vì tự new. Container đăng ký và tạo các service. Singleton chỉ có một đối tượng trong cả ứng dụng, Scoped một đối tượng cho mỗi request, còn Transient tạo mới mỗi lần resolve. Em thường đăng ký DbContext là Scoped. Không nên giữ DbContext Scoped trong một Singleton vì dependency sẽ bị giữ lâu hơn scope của nó.";
    public const string StrongEf="Dạ, khi truy vấn có tracking, DbContext giữ entity trong ChangeTracker với các trạng thái như Added, Modified, Deleted và Unchanged. EF lưu snapshot, rồi DetectChanges so sánh để biết thuộc tính nào thay đổi. Khi gọi SaveChanges, trạng thái quyết định câu lệnh insert, update hay delete. Với màn hình danh sách chỉ đọc, em dùng AsNoTracking để giảm bộ nhớ và chi phí theo dõi. Nếu cần sửa sau đó thì phải attach đúng trạng thái hoặc truy vấn lại có tracking.";
    public const string PartialDi="Dạ, em hiểu DI là truyền đối tượng cần dùng vào constructor của lớp, thay vì tự tạo trong lớp. Em có đăng ký service trong Program bằng AddScoped. Nhưng ba loại vòng đời thì em còn nhầm, em nghĩ Singleton và Scoped đều dùng chung một đối tượng.";
    public const string FollowDi="Nếu hai HTTP request cùng sử dụng một DbContext đăng ký Scoped, chúng có nhận cùng một instance không? Vì sao?";
    public const string PartialFollow="Dạ, em sửa lại là không cùng instance giữa hai request. Trong cùng một request thì các service lấy cùng DbContext. Sang request khác sẽ có DbContext mới. Còn trường hợp Singleton dùng service Scoped thì em chưa giải thích được rủi ro.";
    public const string PartialEf="Dạ, DbContext có theo dõi các entity mà mình truy vấn. Khi đổi một thuộc tính rồi gọi SaveChanges thì EF cập nhật xuống database. Em biết AsNoTracking dành cho trang chỉ xem dữ liệu, nhưng em chưa rõ EF phát hiện thay đổi bằng cách nào và các trạng thái cụ thể.";
    public const string TimeoutMvc="Dạ, MVC gồm Model, View và Controller. Controller nhận request, gọi service lấy dữ liệu rồi đưa sang View để hiển thị. Model chứa dữ liệu cần dùng. Em chưa nhớ rõ bước routing, model binding và validation nằm ở đâu trong luồng này.";
}
