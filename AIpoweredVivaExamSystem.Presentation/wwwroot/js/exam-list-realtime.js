(() => {
    const root = document.querySelector('[data-exam-list-realtime]');
    if (!root) return;
    const state = root.querySelector('[data-connection-state]');
    const setState = (text, live) => {
        state.textContent = text;
        state.classList.toggle('badge-active', live);
        state.classList.toggle('badge-inactive', !live);
    };
    let loading = false, pending = false, timer = null, connection = null, joinedBefore = false;
    function scheduleRefresh() {
        clearTimeout(timer);
        timer = setTimeout(refresh, 300);
    }
    async function refresh() {
        if (loading) { pending = true; return; }
        loading = true;
        try {
            const response = await fetch(location.href, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error('Không lấy được danh sách. Tải lại trang để cập nhật.');
            const html = new DOMParser().parseFromString(await response.text(), 'text/html');
            const next = html.querySelector('[data-exam-list]');
            const current = root.querySelector('[data-exam-list]');
            if (!next || !current) throw new Error('Không lấy được danh sách. Tải lại trang để cập nhật.');
            const known = new Set([...current.querySelectorAll('[data-exam-id]')].map(row => row.dataset.examId));
            current.replaceChildren(...next.childNodes);
            for (const row of current.querySelectorAll('[data-exam-id]')) {
                if (!known.has(row.dataset.examId)) {
                    row.animate([{ backgroundColor: 'var(--success-soft)' }, { backgroundColor: 'transparent' }], { duration: 2500, easing: 'ease-out' });
                }
            }
            if (connection?.state === signalR.HubConnectionState.Connected) setState('Cập nhật trực tiếp', true);
        } catch (error) { setState(error.message, false); }
        finally { loading = false; if (pending) { pending = false; refresh(); } }
    }
    if (!window.signalR) { setState('Không có cập nhật trực tiếp', false); return; }
    connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.hubUrl).withAutomaticReconnect().build();
    async function join() {
        await connection.invoke('JoinExamList');
        setState('Cập nhật trực tiếp', true);
        if (joinedBefore) await refresh();
        joinedBefore = true;
    }
    connection.on('ExamListChanged', scheduleRefresh);
    connection.onreconnecting(() => setState('Đang kết nối lại…', false));
    connection.onreconnected(() => join().catch(() => { setState('Không có quyền nhận cập nhật', false); connection.stop(); }));
    connection.onclose(() => { setState('Mất kết nối. Đang thử lại…', false); setTimeout(start, 5000); });
    async function start() { try { await connection.start(); await join(); } catch { setState('Không kết nối được. Đang thử lại…', false); setTimeout(start, 5000); } }
    start();
})();
