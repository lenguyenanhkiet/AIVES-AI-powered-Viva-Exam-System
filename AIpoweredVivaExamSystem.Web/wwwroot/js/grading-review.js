// Chấm điểm một phiên vấn đáp: tổng điểm giảng viên tính ngay khi nhập, kiểm tra khoảng điểm từng tiêu chí,
// "Dùng gợi ý AI" chỉ chép số vào ô (chưa lưu gì), và cảnh báo khi rời trang lúc còn điểm chưa lưu.
const gradingForm = document.querySelector("[data-grading-form]");
if (gradingForm) {
    const scoreInputs = [...gradingForm.querySelectorAll("[data-score]")];
    const editable = scoreInputs.some((input) => !input.disabled && !input.readOnly);
    const dirtyNote = document.querySelector("[data-dirty-note]");
    let dirty = false;
    let submitting = false;

    const round = (value) => Math.round(value * 100) / 100;
    const format = (value) => String(round(value));

    // "" = chưa nhập; NaN = không phải số.
    const parseScore = (raw) => {
        const text = String(raw ?? "").trim().replace(",", ".");
        if (text === "") return "";
        return /^\d+(\.\d+)?$/.test(text) ? parseFloat(text) : NaN;
    };

    const errorFor = (input) => document.getElementById(input.getAttribute("aria-describedby")?.split(" ").find((id) => id.endsWith("-error")) ?? "");

    const validate = (input) => {
        const value = parseScore(input.value);
        const max = parseFloat(input.dataset.max);
        let message = "";
        if (Number.isNaN(value)) message = "Nhập một số, ví dụ 1.5.";
        else if (value !== "" && (value < 0 || value > max)) message = `Điểm phải từ 0 đến ${format(max)}.`;
        input.classList.toggle("input-validation-error", message !== "");
        input.setAttribute("aria-invalid", message ? "true" : "false");
        input.setCustomValidity(message);
        const error = errorFor(input);
        if (error) error.textContent = message;
        return message === "";
    };

    const recompute = () => {
        let total = 0;
        let entered = 0;
        gradingForm.querySelectorAll("[data-question-card]").forEach((card) => {
            let sum = 0;
            let filled = 0;
            const inputs = card.querySelectorAll("[data-score]");
            inputs.forEach((input) => {
                const value = parseScore(input.value);
                if (typeof value === "number" && !Number.isNaN(value)) {
                    sum += value;
                    filled++;
                }
            });
            const out = card.querySelector("[data-question-total]");
            if (out) out.textContent = filled === 0 ? "—" : format(sum);
            total += sum;
            entered += filled;
        });
        document.querySelectorAll("[data-lecturer-total]").forEach((out) => {
            out.textContent = entered === 0 ? "—" : format(total);
        });
        const missing = scoreInputs.length - entered;
        document.querySelectorAll("[data-missing-count]").forEach((out) => {
            out.textContent = missing === 0 ? "Đã nhập đủ điểm các tiêu chí." : `Còn ${missing} tiêu chí chưa nhập điểm.`;
            out.classList.toggle("is-complete", missing === 0);
        });
    };

    const markDirty = () => {
        if (dirty) return;
        dirty = true;
        if (dirtyNote) dirtyNote.hidden = false;
    };

    gradingForm.addEventListener("input", (event) => {
        if (event.target.matches("[data-score]")) {
            validate(event.target);
            recompute();
        }
        markDirty();
    });

    // Ô số: con lăn chuột không được lặng lẽ đổi điểm đang nhập.
    gradingForm.addEventListener("wheel", (event) => {
        if (event.target.matches("[data-score]") && document.activeElement === event.target) event.target.blur();
    }, { passive: true });

    // Chép gợi ý AI vào các ô của một phạm vi (một câu hoặc cả phiên). Ô giảng viên đã nhập khác gợi ý chỉ bị ghi đè khi xác nhận.
    const takeSuggestions = (scope, label) => {
        const targets = [...scope.querySelectorAll("[data-score]")].filter((input) => input.dataset.suggested !== undefined && input.dataset.suggested !== "" && !input.disabled && !input.readOnly);
        if (targets.length === 0) return;
        const conflicts = targets.filter((input) => {
            const own = parseScore(input.value);
            return own !== "" && own !== parseFloat(input.dataset.suggested);
        });
        if (conflicts.length > 0 &&
            !confirm(`${label}: ${conflicts.length} ô đã có điểm bạn nhập khác gợi ý AI. Ghi đè bằng gợi ý AI?`)) return;
        targets.forEach((input) => {
            input.value = input.dataset.suggested;
            validate(input);
        });
        recompute();
        markDirty();
        targets[0].focus();
        const status = document.querySelector("[data-grading-status]");
        if (status) status.textContent = `Đã chép ${targets.length} điểm gợi ý vào ô điểm giảng viên. Điểm chưa được lưu.`;
    };

    document.addEventListener("click", (event) => {
        const one = event.target.closest("[data-take-ai]");
        if (one) {
            takeSuggestions(one.closest("[data-question-card]"), one.dataset.takeAi || "Câu này");
            return;
        }
        if (event.target.closest("[data-take-ai-all]")) takeSuggestions(gradingForm, "Cả phiên");
    });

    // Chốt điểm: mọi ô hợp lệ; ô trống thì hỏi lại một lần trước khi gửi (server vẫn là nơi quyết định).
    gradingForm.addEventListener("submit", (event) => {
        const invalid = scoreInputs.filter((input) => !validate(input));
        if (invalid.length > 0) {
            event.preventDefault();
            invalid[0].focus();
            invalid[0].reportValidity();
            return;
        }
        const finalizing = event.submitter?.hasAttribute("data-finalize");
        if (finalizing) {
            const empty = scoreInputs.filter((input) => parseScore(input.value) === "").length;
            const question = empty > 0
                ? `Còn ${empty} tiêu chí chưa nhập điểm. Vẫn chốt điểm cho sinh viên này?`
                : "Chốt điểm cho sinh viên này? Sinh viên sẽ xem được điểm và nhận xét.";
            if (!confirm(question)) {
                event.preventDefault();
                return;
            }
        }
        submitting = true;
    });

    window.addEventListener("beforeunload", (event) => {
        if (!dirty || submitting || !editable) return;
        event.preventDefault();
        event.returnValue = "";
    });

    // ---------- Gợi ý AI: yêu cầu, chờ và cập nhật tại chỗ (không bao giờ đụng vào ô điểm giảng viên) ----------
    const cards = [...gradingForm.querySelectorAll("[data-question-card]")];
    cards.forEach((card) => { card.dataset.state = card.dataset.state || "empty"; });
    const token = gradingForm.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";
    const vnTime = (iso) => {
        const date = new Date(iso);
        if (Number.isNaN(date.getTime())) return "";
        const parts = Object.fromEntries(new Intl.DateTimeFormat("en-GB", {
            timeZone: "Asia/Ho_Chi_Minh", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", hourCycle: "h23"
        }).formatToParts(date).map((p) => [p.type, p.value]));
        return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
    };
    const setText = (root, selector, text) => {
        const el = root.querySelector(selector);
        if (el) el.textContent = text;
    };

    const renderSessionAi = () => {
        const ready = cards.filter((card) => card.dataset.state === "ready");
        document.querySelectorAll("[data-take-ai-all]").forEach((btn) => { btn.hidden = ready.length === 0; });
        const chip = document.querySelector("[data-ai-session-chip]");
        if (!chip) return;
        const allReady = cards.length > 0 && ready.length === cards.length;
        chip.hidden = !allReady;
        if (allReady) {
            const sum = cards.reduce((acc, card) => acc + (parseFloat(card.dataset.aiTotal) || 0), 0);
            setText(chip, "[data-ai-session-total]", format(sum));
        }
    };

    const applySuggestion = (card, q) => {
        const state = q.suggestionState || "empty";
        card.dataset.state = state;
        card.querySelectorAll("[data-ai-when]").forEach((el) => { el.hidden = el.dataset.aiWhen !== state; });
        setText(card, "[data-ai-error]", q.suggestionError ? `: ${q.suggestionError.trim().replace(/\.+$/, "")}` : "");
        setText(card, "[data-ai-model]", q.aiModel || "AI");
        setText(card, "[data-ai-at]", q.suggestedAt ? ` · ${vnTime(q.suggestedAt)}` : "");

        (q.criteria || []).forEach((c) => {
            const row = card.querySelector(`[data-criterion-id="${c.id}"]`);
            if (!row) return;
            const has = c.suggestedScore !== null && c.suggestedScore !== undefined;
            const input = row.querySelector("[data-score]");
            if (input) input.dataset.suggested = has ? format(c.suggestedScore) : "";
            const block = row.querySelector("[data-ai-criterion]");
            if (block) block.hidden = !has;
            setText(row, "[data-ai-score]", has ? format(c.suggestedScore) : "—");
            setText(row, "[data-ai-justification]", c.justification || "");
        });

        const feedback = card.querySelector("[data-ai-feedback]");
        if (feedback) {
            feedback.hidden = state !== "ready";
            feedback.querySelectorAll("[data-ai-part]").forEach((part) => {
                const text = (q[part.dataset.aiPart] || "").trim();
                part.hidden = text === "";
                setText(part, "[data-ai-text]", text);
            });
        }

        const hasTotal = q.suggestedTotal !== null && q.suggestedTotal !== undefined;
        card.dataset.aiTotal = hasTotal ? String(q.suggestedTotal) : "";
        const chip = card.querySelector("[data-ai-total-chip]");
        if (chip) chip.hidden = !hasTotal;
        setText(card, "[data-ai-total]", hasTotal ? format(q.suggestedTotal) : "—");
        const take = card.querySelector("[data-take-ai]");
        if (take) take.hidden = state !== "ready";
        renderSessionAi();
    };

    const showPending = (card) => applySuggestion(card, { suggestionState: "pending", criteria: [] });
    const showFailed = (card, message) => applySuggestion(card, { suggestionState: "failed", suggestionError: message, criteria: [] });

    const polling = new Set();
    const poll = (card) => {
        if (polling.has(card)) return;
        polling.add(card);
        const tick = async () => {
            try {
                const response = await fetch(`${gradingForm.dataset.suggestionUrl}&questionId=${encodeURIComponent(card.dataset.questionId)}`, { headers: { Accept: "application/json" } });
                if (response.ok) {
                    const q = await response.json();
                    if (q.suggestionState !== "pending") {
                        polling.delete(card);
                        applySuggestion(card, q);
                        return;
                    }
                }
            } catch (_) { /* mạng chập chờn: thử lại ở lượt sau */ }
            setTimeout(tick, 4000);
        };
        setTimeout(tick, 4000);
    };

    // Một yêu cầu cho một câu; server tự bỏ qua nếu câu đã có gợi ý (regenerate=false).
    const requestSuggestion = async (card, regenerate) => {
        showPending(card);
        const body = new FormData();
        body.append("questionId", card.dataset.questionId);
        body.append("regenerate", regenerate ? "true" : "false");
        body.append("__RequestVerificationToken", token);
        try {
            const response = await fetch(gradingForm.dataset.suggestUrl, { method: "POST", body, headers: { Accept: "application/json", RequestVerificationToken: token } });
            const data = await response.json().catch(() => null);
            if (!response.ok || !data) {
                showFailed(card, data?.error || "máy chủ không trả lời");
                return;
            }
            applySuggestion(card, data);
            if (data.suggestionState === "pending") poll(card);
        } catch (_) {
            showFailed(card, "mất kết nối tới máy chủ");
        }
    };

    gradingForm.addEventListener("click", (event) => {
        const button = event.target.closest("[data-ai-request]");
        if (!button) return;
        const card = button.closest("[data-question-card]");
        const regenerate = button.dataset.aiRequest === "regenerate";
        if (regenerate && card.dataset.state === "ready" &&
            !confirm("Tạo lại gợi ý AI cho câu này? Gợi ý cũ sẽ được thay; điểm bạn đã nhập giữ nguyên.")) return;
        requestSuggestion(card, regenerate);
    });

    cards.forEach((card) => {
        const total = card.querySelector("[data-ai-total]")?.textContent.trim();
        card.dataset.aiTotal = total && total !== "—" ? total : "";
        if (card.dataset.state === "pending") poll(card);
    });
    renderSessionAi();

    // Câu chưa có gợi ý: tự yêu cầu sau khi trang đã hiện, tối đa 2 câu cùng lúc.
    if (gradingForm.dataset.autoSuggest === "true") {
        const queue = cards.filter((card) => card.dataset.state === "empty");
        const worker = async () => {
            for (let card = queue.shift(); card; card = queue.shift()) await requestSuggestion(card, false);
        };
        worker();
        worker();
    }

    // Bản ghi WebM từ MediaRecorder không ghi độ dài nên thanh tua không dùng được: tua tới cuối một lần để trình duyệt tính độ dài.
    document.querySelectorAll(".grade-audio audio").forEach((audio) => {
        audio.addEventListener("loadedmetadata", () => {
            if (audio.duration !== Infinity) return;
            const resume = !audio.paused;
            const fix = () => {
                if (audio.duration === Infinity) return;
                audio.removeEventListener("durationchange", fix);
                audio.currentTime = 0;
                if (resume) audio.play().catch(() => { });
            };
            audio.addEventListener("durationchange", fix);
            audio.currentTime = 1e101;
        });
    });

    scoreInputs.forEach((input) => {
        if (input.value !== "") validate(input);
    });
    recompute();

    // Server trả lại trang vì lỗi: các giá trị trên trang chưa được lưu, nên vẫn cảnh báo khi rời trang; đưa con trỏ tới ô lỗi đầu tiên.
    const firstError = gradingForm.querySelector(".input-validation-error");
    if (firstError || document.querySelector(".validation-summary-errors")) markDirty();
    if (firstError) {
        firstError.focus({ preventScroll: true });
        firstError.scrollIntoView({ block: "center" });
    }
}
