(() => {
    const root = document.querySelector('[data-exam-realtime]');
    if (!root) return;
    const state = root.querySelector('[data-connection-state]');
    const status = root.querySelector('[data-exam-status]');
    let loading = false, pending = false;
    async function refresh() {
        if (loading) { pending = true; return; }
        loading = true;
        try {
            const response = await fetch(root.dataset.statusUrl, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error('Không có quyền hoặc không lấy được trạng thái');
            const data = await response.json();
            status.textContent = `Trạng thái: ${data.status}. Câu hỏi trong pool: ${data.counts.poolQuestions}. Sinh viên: ${data.counts.students}. Đã xếp lịch: ${data.counts.scheduledStudents}. Phiên thi: ${data.counts.sessions}. Phiên đã hoàn thành: ${data.completedSessions}.`;
            const roster = root.querySelector('[data-exam-roster]');
            roster.replaceChildren(...data.roster.map(student => {
                const row = document.createElement('tr');
                const slot = student.scheduledAt ? new Date(student.scheduledAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa xếp lịch';
                for (const value of [student.name, slot, student.sessionStatus]) {
                    const cell = document.createElement('td'); cell.textContent = value; row.append(cell);
                }
                return row;
            }));
        } catch (error) { state.textContent = error.message; }
        finally { loading = false; if (pending) { pending = false; refresh(); } }
    }
    refresh();
    if (!window.signalR) { state.textContent = 'Không có cập nhật trực tiếp. Tải lại trang để cập nhật.'; return; }
    const connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.hubUrl).withAutomaticReconnect().build();
    async function join() {
        await connection.invoke('JoinExam', root.dataset.examId);
        state.textContent = 'Đã kết nối cập nhật trực tiếp';
        await refresh();
    }
    connection.on('ExamChanged', id => { if (id.toLowerCase() === root.dataset.examId.toLowerCase()) refresh(); });
    connection.onreconnecting(() => { state.textContent = 'Đang kết nối lại…'; });
    connection.onreconnected(() => join().catch(() => { state.textContent = 'Không có quyền truy cập'; connection.stop(); }));
    connection.onclose(() => { state.textContent = 'Mất kết nối. Đang thử lại…'; setTimeout(start, 5000); });
    async function start() { try { await connection.start(); await join(); } catch { state.textContent = 'Không kết nối được. Đang thử lại…'; setTimeout(start, 5000); } }
    start();
})();
