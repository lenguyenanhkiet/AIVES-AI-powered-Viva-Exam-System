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
            if (!response.ok || response.redirected) throw new Error('Access denied or status unavailable');
            const data = await response.json();
            status.textContent = `Status: ${data.status}. Pool: ${data.counts.poolQuestions}. Students: ${data.counts.students}. Scheduled: ${data.counts.scheduledStudents}. Sessions: ${data.counts.sessions}. Completed sessions: ${data.completedSessions}.`;
            const roster = root.querySelector('[data-exam-roster]');
            roster.replaceChildren(...data.roster.map(student => {
                const row = document.createElement('tr');
                const slot = student.scheduledAt ? new Date(student.scheduledAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Not scheduled';
                for (const value of [student.name, slot, student.sessionStatus]) {
                    const cell = document.createElement('td'); cell.textContent = value; row.append(cell);
                }
                return row;
            }));
        } catch (error) { state.textContent = error.message; }
        finally { loading = false; if (pending) { pending = false; refresh(); } }
    }
    refresh();
    if (!window.signalR) { state.textContent = 'Realtime unavailable. Reload to update.'; return; }
    const connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.hubUrl).withAutomaticReconnect().build();
    async function join() {
        await connection.invoke('JoinExam', root.dataset.examId);
        state.textContent = 'Live updates connected';
        await refresh();
    }
    connection.on('ExamChanged', id => { if (id.toLowerCase() === root.dataset.examId.toLowerCase()) refresh(); });
    connection.onreconnecting(() => { state.textContent = 'Reconnecting…'; });
    connection.onreconnected(() => join().catch(() => { state.textContent = 'Access denied'; connection.stop(); }));
    connection.onclose(() => { state.textContent = 'Disconnected. Retrying…'; setTimeout(start, 5000); });
    async function start() { try { await connection.start(); await join(); } catch { state.textContent = 'Connection unavailable. Retrying…'; setTimeout(start, 5000); } }
    start();
})();
