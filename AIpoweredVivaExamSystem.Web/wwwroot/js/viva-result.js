(() => {
    const root = document.getElementById('viva-student-result');
    if (!root) return;
    const content = root.querySelector('[data-result-content]');
    const status = root.querySelector('[data-result-connection]');
    const fallback = 'Không thể cập nhật tự động. Tải lại trang để xem kết quả mới nhất.';
    if (!window.signalR) { status.textContent = fallback; return; }

    let loading = false, pending = false, stopped = false, starting = false, retry = 0;
    async function refresh(invalidate = false) {
        // Hide a possibly reopened result as soon as the invalidation arrives.
        if (invalidate) content.replaceChildren();
        if (loading) { pending = true; return; }
        loading = true;
        try {
            const response = await fetch(root.dataset.resultUrl, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error('Result unavailable');
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const result = page.querySelector('#viva-student-result [data-result-content]');
            if (!result) throw new Error('Result unavailable');
            // A later invalidation arrived during this fetch: discard this response and
            // refetch rather than briefly restoring the old finalised result.
            if (!pending) {
                content.replaceChildren(...Array.from(result.childNodes));
                status.textContent = 'Kết quả đang được cập nhật tự động.';
            }
        } catch {
            status.textContent = fallback;
        } finally {
            loading = false;
            if (pending) { pending = false; refresh(); }
        }
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl).withAutomaticReconnect().build();
    async function join() {
        await connection.invoke('JoinSession', root.dataset.sessionId);
        // Covers changes between page render and subscription, and while disconnected.
        await refresh(true);
    }
    connection.on('StudentResultChanged', id => {
        if (id.toLowerCase() === root.dataset.sessionId.toLowerCase()) refresh(true);
    });
    connection.onreconnecting(() => { status.textContent = 'Đang kết nối lại để cập nhật kết quả…'; });
    connection.onreconnected(() => join().catch(() => { status.textContent = fallback; connection.stop(); }));
    function schedule() {
        if (!stopped) { clearTimeout(retry); retry = setTimeout(start, 5000); }
    }
    connection.onclose(() => { status.textContent = fallback; schedule(); });
    async function start() {
        if (stopped || starting) return;
        starting = true;
        try { await connection.start(); await join(); }
        catch {
            status.textContent = fallback;
            await connection.stop().catch(() => {});
            schedule();
        }
        finally { starting = false; }
    }
    window.addEventListener('pagehide', () => {
        stopped = true;
        clearTimeout(retry);
        connection.stop().catch(() => {});
    });
    start();
})();
