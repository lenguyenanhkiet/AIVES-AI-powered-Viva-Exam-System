// Logic phòng thi vấn đáp AI (AIVES Core — Group 3):
// 1. Giám khảo AI đọc câu hỏi: audio từ server (/VivaSessions/Speak), dự phòng bằng speechSynthesis của trình duyệt.
// 2. Ghi âm câu trả lời (MediaRecorder, tạm dừng/nói tiếp trong cùng một file) + chữ trực tiếp
//    (ElevenLabs Scribe realtime, dự phòng Web Speech API).
// 3. Nhận diện bản ghi âm phía Server (/VivaSessions/Transcribe) khi không có chữ trực tiếp.
// 4. Đồng hồ lượt/ca; hết giờ thì nộp đúng phần đã có kèm timedOut=true, không tự thêm nội dung.
// Dải trạng thái (#viva-phase-*) luôn cho biết đang đến lượt ai: Giám khảo đọc / bạn trả lời / ghi âm / AI phân tích.
(function () {
    "use strict";

    const root = document.getElementById("viva-interview-root");
    if (!root) return;

    const byId = function (id) { return document.getElementById(id); };

    const isCompleted = root.dataset.isCompleted === "true";
    const sessionId = root.dataset.sessionId || "";
    const activeTurnId = root.dataset.turnId || "";
    const speakUrl = root.dataset.speakUrl || "/VivaSessions/Speak";
    const transcribeUrl = root.dataset.transcribeUrl || "/VivaSessions/Transcribe";
    const scribeTokenUrl = root.dataset.scribeTokenUrl || "/VivaSessions/ScribeToken";
    const turnLimitSeconds = parseInt(root.dataset.turnLimitSeconds || "0", 10);
    const pageLoadedAt = Date.now();
    const turnDeadline = pageLoadedAt + parseInt(root.dataset.remainingTurnSeconds || "0", 10) * 1000;
    const slotDeadline = pageLoadedAt + parseInt(root.dataset.remainingSlotSeconds || "0", 10) * 1000;

    const activeCard = byId("viva-active-card");
    const phaseTitle = byId("viva-phase-title");
    const phaseDetail = byId("viva-phase-detail");
    const turnTimerEl = byId("turn-timer");
    const turnTimerBox = byId("turn-timer-box");
    const turnTimerBar = byId("turn-timer-bar");
    const slotTimerEl = byId("slot-timer");
    const slotTimerBox = byId("slot-timer-box");
    const timerAnnouncer = byId("timer-announcer");
    const langSelect = byId("viva-lang-select");
    const inputLanguage = byId("input-language");
    const btnSpeakActive = byId("btn-speak-active");
    const voiceBar = byId("viva-voice-bar");
    const btnToggleMic = byId("btn-toggle-mic");
    const micBtnLabel = byId("mic-btn-label");
    const recIndicator = byId("rec-indicator");
    const recElapsed = byId("rec-elapsed");
    const recLevel = byId("rec-level");
    const btnServerStt = byId("btn-server-stt");
    const audioPreviewWrap = byId("audio-preview-wrap");
    const audioPreviewPlayer = byId("audio-preview-player");
    const inputAudioFile = byId("input-audio-file");
    const inputAudioDuration = byId("input-audio-duration");
    const inputTimedOut = byId("input-timed-out");
    const transcriptInput = byId("transcript-input");
    const answerForm = byId("viva-answer-form");
    const btnSubmitAnswer = byId("btn-submit-answer");
    const submitBtnLabel = byId("submit-btn-label");

    const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition;
    const canRecord = !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && typeof MediaRecorder !== "undefined");
    const canRecognize = !!SpeechRecognitionCtor;

    // Trạng thái hiển thị: examiner = giọng Giám khảo, student = thao tác của sinh viên.
    let examinerState = "idle";   // idle | loading | speaking | blocked
    let studentState = "idle";    // idle | recording | transcribing | submitting | timeout-turn | timeout-slot
    let notice = null;            // { text, tone: "info" | "error" } — thay cho dòng hướng dẫn tới thao tác kế tiếp
    let isSubmitting = false;

    function formatSeconds(totalSeconds) {
        const safe = Math.max(0, Math.floor(totalSeconds));
        return String(Math.floor(safe / 60)).padStart(2, "0") + ":" + String(safe % 60).padStart(2, "0");
    }

    function getSelectedLang() {
        return langSelect ? langSelect.value : "vi-VN";
    }

    function syncShortLanguageInput() {
        if (inputLanguage) {
            inputLanguage.value = getSelectedLang().toLowerCase().startsWith("en") ? "en" : "vi";
        }
    }

    if (langSelect) {
        const savedLang = sessionStorage.getItem("aives_viva_lang");
        if (savedLang === "vi-VN" || savedLang === "en-US") {
            langSelect.value = savedLang;
        }
        syncShortLanguageInput();
        langSelect.addEventListener("change", function () {
            sessionStorage.setItem("aives_viva_lang", langSelect.value);
            syncShortLanguageInput();
        });
    }

    // ---------------- Dải trạng thái phòng thi ----------------
    function answerHint() {
        const liveLikely = !scribeUnavailable || (canRecognize && !browserSpeechFailed);
        if (canRecord && liveLikely) {
            return "Bấm “Bắt đầu trả lời” rồi nói, lời nói sẽ hiện thành văn bản trong ô bên dưới. Bạn cũng có thể gõ câu trả lời.";
        }
        if (canRecord) {
            return "Trình duyệt này không chuyển giọng nói thành chữ trực tiếp. Bấm “Bắt đầu trả lời” để ghi âm; khi bạn dừng, AI Server sẽ nhận diện bản ghi âm. Bạn cũng có thể gõ câu trả lời.";
        }
        if (canRecognize) {
            return "Bấm “Bắt đầu trả lời” rồi nói, lời nói sẽ hiện thành văn bản trong ô bên dưới (trình duyệt này không lưu được bản ghi âm).";
        }
        return "Trình duyệt này không hỗ trợ trả lời bằng giọng nói. Hãy gõ câu trả lời vào ô bên dưới, hoặc mở phiên thi bằng Chrome/Edge để trả lời bằng giọng nói.";
    }

    function currentPhase() {
        if (studentState !== "idle") return studentState;
        if (examinerState !== "idle") return examinerState;
        return "answer";
    }

    function phaseText(phase) {
        switch (phase) {
            case "loading":
                return ["Giám khảo AI chuẩn bị đọc câu hỏi…", "Bạn có thể đọc câu hỏi bên dưới trong lúc chờ."];
            case "speaking":
                return ["Giám khảo AI đang đọc câu hỏi", "Hãy lắng nghe. Khi bạn bắt đầu trả lời, giọng đọc sẽ dừng lại."];
            case "blocked":
                return ["Bấm “Nghe câu hỏi” để Giám khảo AI đọc", "Trình duyệt chỉ phát âm thanh sau khi bạn bấm vào trang. Bạn cũng có thể đọc câu hỏi bên dưới và trả lời ngay."];
            case "recording":
                return ["Đang ghi âm câu trả lời của bạn", recognitionActive()
                    ? "Nói rõ ràng, lời nói hiện thành văn bản bên dưới. Bấm “Dừng ghi âm” khi nói xong."
                    : "Nói rõ ràng. Khi bạn bấm “Dừng ghi âm”, AI Server sẽ chuyển bản ghi âm thành văn bản."];
            case "transcribing":
                return ["AI Server đang nhận diện bản ghi âm…", "Văn bản sẽ hiện trong ô câu trả lời sau ít giây."];
            case "submitting":
                return ["Đang nộp câu trả lời…", "Hệ thống đang chuẩn bị lượt tiếp theo. Vui lòng chờ, đừng tải lại trang."];
            case "timeout-turn":
                return ["Hết thời gian trả lời lượt này", "Hệ thống đang nộp đúng phần bạn đã có (văn bản và bản ghi âm nếu có). Lượt này được ghi nhận là hết giờ, không có câu hỏi xoáy, và phiên thi chuyển sang câu hỏi chính tiếp theo."];
            case "timeout-slot":
                return ["Hết thời gian ca thi", "Hệ thống đang nộp đúng phần bạn đã có (văn bản và bản ghi âm nếu có) và kết thúc phiên thi."];
            default:
                return ["Đến lượt bạn trả lời", answerHint()];
        }
    }

    function render() {
        const phase = currentPhase();
        const text = phaseText(phase);
        const showNotice = notice && !isSubmitting;
        if (activeCard) {
            activeCard.dataset.phase = phase;
            activeCard.dataset.notice = showNotice ? notice.tone : "";
        }
        if (phaseTitle) phaseTitle.textContent = text[0];
        if (phaseDetail) phaseDetail.textContent = showNotice ? notice.text : text[1];
        renderSpeakButtons();
        renderMicButton();
        renderSubmitButton();
    }

    function setNotice(text, tone) {
        notice = text ? { text: text, tone: tone || "info" } : null;
        render();
    }

    // ---------------- 1. Giọng Giám khảo AI ----------------
    // Audio server được giữ theo từng lượt để nghe lại không phải tải lại; lượt nào server không có giọng thì
    // đánh dấu để lần sau đọc ngay bằng trình duyệt.
    const SERVER_VOICE_TIMEOUT_MS = 12000;
    const VOICE_UNAVAILABLE = "unavailable";
    const voiceCache = new Map();
    let voiceToken = 0;
    let currentAudio = null;
    let speakingButton = null;

    function setExaminer(state) {
        examinerState = state;
        render();
    }

    function markSpoken(turnId) {
        if (turnId && turnId === activeTurnId) {
            sessionStorage.setItem("aives_last_spoken_turn", turnId);
        }
    }

    function stopExaminerVoice() {
        voiceToken++;
        if (currentAudio) {
            currentAudio.pause();
            currentAudio = null;
        }
        if ("speechSynthesis" in window) {
            window.speechSynthesis.cancel();
        }
        if (examinerState !== "idle") setExaminer("idle");
    }

    function speakWithBrowser(turnId, text, token) {
        if (!("speechSynthesis" in window) || !text.trim()) {
            examinerState = "idle";
            setNotice("Máy này không phát được giọng đọc. Hãy đọc câu hỏi bên dưới.", "error");
            return;
        }

        const utterance = new SpeechSynthesisUtterance(text.trim());
        const targetLang = getSelectedLang();
        utterance.lang = targetLang;
        utterance.rate = 1.0;

        const voices = window.speechSynthesis.getVoices();
        const langPrefix = targetLang.split("-")[0].toLowerCase();
        const matchedVoice =
            voices.find(function (v) { return v.lang && v.lang.toLowerCase() === targetLang.toLowerCase(); }) ||
            voices.find(function (v) { return v.lang && v.lang.toLowerCase().startsWith(langPrefix); });
        if (matchedVoice) {
            utterance.voice = matchedVoice;
        }

        const startGuard = setTimeout(function () {
            if (token === voiceToken && examinerState === "loading") setExaminer("idle");
        }, 4000);
        utterance.onstart = function () {
            clearTimeout(startGuard);
            if (token !== voiceToken) return;
            markSpoken(turnId);
            setExaminer("speaking");
        };
        utterance.onend = function () {
            if (token === voiceToken) setExaminer("idle");
        };
        utterance.onerror = function (ev) {
            clearTimeout(startGuard);
            if (token !== voiceToken) return;
            setExaminer(ev && ev.error === "not-allowed" ? "blocked" : "idle");
        };

        setExaminer("loading");
        window.speechSynthesis.speak(utterance);
    }

    function playExaminerVoice(turnId, text, button) {
        stopExaminerVoice();
        const token = voiceToken;
        speakingButton = button || null;

        if (!turnId || voiceCache.get(turnId) === VOICE_UNAVAILABLE) {
            speakWithBrowser(turnId, text, token);
            return;
        }

        let audio = voiceCache.get(turnId);
        if (!audio) {
            audio = new Audio(speakUrl + "?sessionId=" + encodeURIComponent(sessionId) + "&turnId=" + encodeURIComponent(turnId));
            audio.preload = "auto";
            voiceCache.set(turnId, audio);
        } else if (audio.readyState > 0) {
            audio.currentTime = 0;
        }
        currentAudio = audio;
        setExaminer("loading");

        let settled = false;
        let timer = 0;
        const settle = function () {
            settled = true;
            clearTimeout(timer);
            audio.removeEventListener("error", onLoadError);
        };
        // Server lỗi, trả non-2xx hoặc quá lâu: bỏ audio này và đọc bằng trình duyệt.
        const fallBack = function (permanent) {
            if (settled) return;
            settle();
            audio.pause();
            audio.removeAttribute("src");
            audio.load();
            if (permanent) {
                voiceCache.set(turnId, VOICE_UNAVAILABLE);
            } else {
                voiceCache.delete(turnId);
            }
            if (token !== voiceToken) return;
            currentAudio = null;
            speakWithBrowser(turnId, text, token);
        };
        const onLoadError = function () { fallBack(true); };

        timer = setTimeout(function () { fallBack(false); }, SERVER_VOICE_TIMEOUT_MS);
        audio.addEventListener("error", onLoadError);
        audio.onended = function () {
            if (token !== voiceToken) return;
            currentAudio = null;
            setExaminer("idle");
        };

        audio.play().then(function () {
            if (settled) return;
            settle();
            if (token !== voiceToken) {
                audio.pause();
                return;
            }
            markSpoken(turnId);
            setExaminer("speaking");
        }).catch(function (err) {
            if (settled) return;
            const name = err && err.name;
            if (name === "NotAllowedError") {
                // Trình duyệt chặn tự phát: giữ audio đã tải, chờ sinh viên bấm "Nghe câu hỏi".
                settle();
                if (token === voiceToken) {
                    currentAudio = null;
                    setExaminer("blocked");
                }
            } else if (name === "AbortError" && token !== voiceToken) {
                settle();
            } else {
                fallBack(true);
            }
        });
    }

    function renderSpeakButtons() {
        const busy = examinerState === "loading" || examinerState === "speaking";
        document.querySelectorAll(".js-speak").forEach(function (btn) {
            const label = btn.querySelector(".js-speak-label");
            if (label && !btn.dataset.idleLabel) {
                btn.dataset.idleLabel = label.textContent;
            }
            const isActive = busy && btn === speakingButton;
            if (label) {
                label.textContent = isActive ? "Dừng đọc" : btn.dataset.idleLabel;
            }
            btn.setAttribute("aria-pressed", isActive ? "true" : "false");
        });
        if (btnSpeakActive) {
            const attention = examinerState === "blocked";
            btnSpeakActive.classList.toggle("btn-primary", attention);
            btnSpeakActive.classList.toggle("btn-secondary", !attention);
            btnSpeakActive.classList.toggle("viva-attention", attention);
        }
    }

    document.querySelectorAll(".js-speak").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const busy = examinerState === "loading" || examinerState === "speaking";
            if (busy && btn === speakingButton) {
                stopExaminerVoice();
                return;
            }
            playExaminerVoice(btn.getAttribute("data-turn-id") || "", btn.getAttribute("data-tts-text") || "", btn);
        });
    });

    // Tự đọc câu hỏi mới đúng một lần mỗi lượt (không đọc lại khi tải lại trang sau khi đã đọc được).
    if (!isCompleted && btnSpeakActive && activeTurnId &&
        sessionStorage.getItem("aives_last_spoken_turn") !== activeTurnId) {
        // Run after initialization, without adding a delay before loading the next voice.
        Promise.resolve().then(function () {
            if (studentState === "idle") {
                playExaminerVoice(activeTurnId, btnSpeakActive.getAttribute("data-tts-text") || "", btnSpeakActive);
            }
        });
    }

    // ---------------- 2. Ghi âm & chữ trực tiếp ----------------
    // Một MediaRecorder cho cả lượt: "Dừng ghi âm" chỉ tạm dừng, "Nói tiếp" ghi nối vào cùng file,
    // nên file nộp lên chứa toàn bộ câu trả lời. Recorder chỉ dừng hẳn khi nộp.
    // Chữ trực tiếp: ElevenLabs Scribe realtime (token dùng một lần từ server) → Web Speech của trình duyệt
    // → chỉ ghi âm, AI Server nhận diện khi dừng. Đổi nguồn giữa chừng không làm mất chữ đã có trong ô.
    const SCRIBE_SOCKET_URL = "wss://api.elevenlabs.io/v1/speech-to-text/realtime";
    const SCRIBE_SAMPLE_RATE = 16000;
    const SCRIBE_CHUNK_SAMPLES = 3200;      // 200 ms mỗi gói gửi lên
    const SCRIBE_CONNECT_TIMEOUT_MS = 6000;
    const SCRIBE_COMMIT_WAIT_MS = 2000;

    let isRecording = false;
    let isStarting = false;
    let recorder = null;
    let mediaStream = null;
    let audioChunks = [];
    let audioExt = "webm";
    let lastAudioBlob = null;
    let previewUrl = "";
    let recordedMs = 0;
    let segmentStartedAt = 0;
    let textBeforeRecording = "";
    let audioGraph = null;          // { ctx, source, analyser, data, processor, frame }
    let pcmSink = null;             // nhận PCM 16 kHz khi Scribe đang chạy
    let resampleCarry = null;
    let live = null;                // nguồn chữ trực tiếp đang chạy: { kind, stop(graceful) }
    let scribeUnavailable = false;  // server không cấp được token: không thử lại trong trang này
    let browserSpeechFailed = false;
    let liveLost = false;           // lần ghi này không còn chữ trực tiếp
    let liveProducedText = false;   // chữ trực tiếp đã ghi được gì vào ô trong cả lượt chưa
    let segBase = "";
    let segFinal = "";
    let segPartial = "";
    let sttAbort = null;

    const canCapture = !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);

    function recognitionActive() {
        return !!live;
    }

    function pickSupportedMimeType() {
        const candidates = [
            { mime: "audio/webm;codecs=opus", ext: "webm" },
            { mime: "audio/webm", ext: "webm" },
            { mime: "audio/ogg;codecs=opus", ext: "ogg" },
            { mime: "audio/mp4", ext: "m4a" }
        ];
        for (const item of candidates) {
            if (MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported(item.mime)) {
                return item;
            }
        }
        return { mime: "", ext: "webm" };
    }

    function joinText(a, b) {
        return [a, b].map(function (s) { return (s || "").trim(); }).filter(Boolean).join(" ");
    }

    function hasAudio() {
        return !!(lastAudioBlob && lastAudioBlob.size > 0);
    }

    // Câu trả lời chỉ bằng bản ghi âm là hợp lệ (server tự nhận diện), nên chỉ bắt buộc nhập văn bản khi chưa ghi âm.
    function syncTranscriptRequired() {
        if (transcriptInput) {
            transcriptInput.required = !(recorder || hasAudio());
        }
    }

    // Khóa ô văn bản khi chữ trực tiếp đang ghi vào, để phần sinh viên gõ không bị ghi đè.
    function syncTranscriptLock() {
        if (transcriptInput) transcriptInput.readOnly = !!live;
    }

    function antiforgeryHeaders() {
        const tokenInput = answerForm ? answerForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        const headers = { "X-Requested-With": "XMLHttpRequest" };
        if (tokenInput && tokenInput.value) {
            headers["RequestVerificationToken"] = tokenInput.value;
        }
        return headers;
    }

    function micErrorMessage(err) {
        const name = err && err.name;
        if (name === "NotAllowedError" || name === "SecurityError") {
            return "Bạn chưa cho phép dùng micro. Bấm biểu tượng ổ khóa cạnh thanh địa chỉ, chọn Cho phép micro, rồi bấm “Bắt đầu trả lời” lại. Trong lúc đó bạn có thể gõ câu trả lời.";
        }
        if (name === "NotFoundError" || name === "OverconstrainedError") {
            return "Không tìm thấy micro trên máy. Hãy kết nối micro rồi thử lại, hoặc gõ câu trả lời.";
        }
        if (name === "NotReadableError" || name === "AbortError") {
            return "Micro đang bị ứng dụng khác sử dụng. Hãy đóng ứng dụng đó rồi thử lại, hoặc gõ câu trả lời.";
        }
        return "Không mở được micro. Bạn vẫn có thể gõ câu trả lời.";
    }

    // --- Văn bản trực tiếp: phần đã có + phần đã chốt + phần tạm thời của đoạn đang nói ---
    function beginLiveText() {
        segBase = transcriptInput ? transcriptInput.value.trim() : "";
        segFinal = "";
        segPartial = "";
    }

    function writeLiveText() {
        if (!transcriptInput) return;
        transcriptInput.value = joinText(segBase, joinText(segFinal, segPartial));
        transcriptInput.scrollTop = transcriptInput.scrollHeight;
        if (segFinal || segPartial) liveProducedText = true;
    }

    // --- Âm thanh micro: một AudioContext cho đồng hồ mức âm và luồng PCM gửi Scribe ---
    // Tạo ngay trong thao tác bấm (trước khi chờ quyền micro) để trình duyệt không treo AudioContext.
    function ensureAudioContext() {
        if (audioGraph) return;
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtx) return;
        try {
            audioGraph = { ctx: new AudioCtx(), source: null, analyser: null, data: null, processor: null, frame: 0 };
        } catch (_) {
            audioGraph = null;
        }
    }

    function connectAudioGraph(stream) {
        const g = audioGraph;
        if (!g || g.source) return;
        try {
            g.source = g.ctx.createMediaStreamSource(stream);
            g.analyser = g.ctx.createAnalyser();
            g.analyser.fftSize = 512;
            g.data = new Uint8Array(g.analyser.fftSize);
            g.source.connect(g.analyser);
            if (g.ctx.createScriptProcessor) {
                g.processor = g.ctx.createScriptProcessor(4096, 1, 1);
                g.processor.onaudioprocess = function (ev) {
                    if (isRecording && pcmSink) {
                        pcmSink(downsampleTo16k(ev.inputBuffer.getChannelData(0), g.ctx.sampleRate));
                    }
                };
                g.source.connect(g.processor);
                g.processor.connect(g.ctx.destination);
            }
            if (g.ctx.state === "suspended") g.ctx.resume().catch(function () { });
        } catch (_) {
            g.processor = null;
        }
    }

    function downsampleTo16k(input, inRate) {
        let data = input;
        if (resampleCarry && resampleCarry.length) {
            data = new Float32Array(resampleCarry.length + input.length);
            data.set(resampleCarry);
            data.set(input, resampleCarry.length);
        }
        const ratio = inRate / SCRIBE_SAMPLE_RATE;
        const outLen = Math.floor(data.length / ratio);
        const out = new Int16Array(outLen);
        for (let i = 0; i < outLen; i++) {
            const start = Math.floor(i * ratio);
            const end = Math.min(data.length, Math.floor((i + 1) * ratio));
            let sample = data[start];
            if (end - start > 1) {
                let sum = 0;
                for (let j = start; j < end; j++) sum += data[j];
                sample = sum / (end - start);
            }
            const s = Math.max(-1, Math.min(1, sample));
            out[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
        }
        resampleCarry = data.slice(Math.floor(outLen * ratio));
        return out;
    }

    function pcmToBase64(samples) {
        const bytes = new Uint8Array(samples.buffer, samples.byteOffset, samples.byteLength);
        let binary = "";
        for (let i = 0; i < bytes.length; i += 0x8000) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }
        return btoa(binary);
    }

    function runLevelMeter() {
        const g = audioGraph;
        if (!g || !g.analyser || !recLevel) return;
        cancelAnimationFrame(g.frame);
        const tick = function () {
            if (audioGraph !== g || !isRecording) {
                recLevel.style.transform = "scaleX(0)";
                return;
            }
            g.analyser.getByteTimeDomainData(g.data);
            let sum = 0;
            for (let i = 0; i < g.data.length; i++) {
                const v = (g.data[i] - 128) / 128;
                sum += v * v;
            }
            const level = Math.min(1, Math.sqrt(sum / g.data.length) * 4);
            recLevel.style.transform = "scaleX(" + level.toFixed(3) + ")";
            g.frame = requestAnimationFrame(tick);
        };
        g.frame = requestAnimationFrame(tick);
    }

    function releaseMicrophone() {
        pcmSink = null;
        if (audioGraph) {
            cancelAnimationFrame(audioGraph.frame);
            if (audioGraph.processor) audioGraph.processor.onaudioprocess = null;
            audioGraph.ctx.close().catch(function () { });
            audioGraph = null;
        }
        if (mediaStream) {
            mediaStream.getTracks().forEach(function (t) { t.stop(); });
            mediaStream = null;
        }
    }

    // --- Nguồn 1: ElevenLabs Scribe realtime ---
    function fetchScribeToken() {
        const formData = new FormData();
        formData.append("sessionId", sessionId);
        formData.append("language", inputLanguage ? inputLanguage.value : "vi");
        const abort = typeof AbortController !== "undefined" ? new AbortController() : null;
        const timer = setTimeout(function () { if (abort) abort.abort(); }, SCRIBE_CONNECT_TIMEOUT_MS);
        return fetch(scribeTokenUrl, {
            method: "POST",
            headers: antiforgeryHeaders(),
            body: formData,
            signal: abort ? abort.signal : undefined
        }).then(function (resp) {
            return resp.ok ? resp.json() : null;
        }).then(function (data) {
            return data && data.token ? data : null;
        }).catch(function () {
            return null;
        }).finally(function () {
            clearTimeout(timer);
        });
    }

    function startScribe() {
        const ctrl = { kind: "scribe", ws: null, queue: [], pending: [], pendingSamples: 0, heard: false, closing: false, done: false, commitWaiter: null };
        live = ctrl;
        resampleCarry = null;
        beginLiveText();

        const send = function (message) {
            if (ctrl.ws && ctrl.ws.readyState === WebSocket.OPEN) {
                ctrl.ws.send(message);
            } else {
                ctrl.queue.push(message);
            }
        };
        const flushPcm = function (commit) {
            let samples;
            if (ctrl.pendingSamples > 0) {
                samples = new Int16Array(ctrl.pendingSamples);
                let offset = 0;
                ctrl.pending.forEach(function (part) { samples.set(part, offset); offset += part.length; });
            } else if (commit) {
                samples = new Int16Array(SCRIBE_SAMPLE_RATE / 10);   // 100 ms im lặng để chốt đoạn cuối
            } else {
                return;
            }
            ctrl.pending = [];
            ctrl.pendingSamples = 0;
            send(JSON.stringify({
                message_type: "input_audio_chunk",
                audio_base_64: pcmToBase64(samples),
                commit: !!commit,
                sample_rate: SCRIBE_SAMPLE_RATE
            }));
        };
        const close = function () {
            ctrl.done = true;
            ctrl.closing = true;
            if (pcmSink === sink) pcmSink = null;
            if (live === ctrl) live = null;
            if (ctrl.commitWaiter) ctrl.commitWaiter();
            if (ctrl.ws) {
                try { ctrl.ws.close(); } catch (_) { }
            }
            syncTranscriptLock();
        };
        const fail = function () {
            if (ctrl.done) return;
            const wasLive = ctrl.heard;
            close();
            if (/-$/.test(segPartial)) {
                // Giữ phần tạm thời đã hiện, bỏ dấu "-" Scribe dùng cho từ chưa nói hết.
                segPartial = segPartial.replace(/-+$/, "");
                writeLiveText();
            }
            onLiveFailed(wasLive);
        };
        const sink = function (samples) {
            if (ctrl.done || ctrl.closing) return;
            ctrl.pending.push(samples);
            ctrl.pendingSamples += samples.length;
            if (ctrl.pendingSamples >= SCRIBE_CHUNK_SAMPLES) flushPcm(false);
        };
        pcmSink = sink;

        ctrl.stop = function (graceful) {
            if (ctrl.done) return Promise.resolve();
            if (pcmSink === sink) pcmSink = null;
            if (!graceful || !ctrl.ws || ctrl.ws.readyState !== WebSocket.OPEN) {
                close();
                return Promise.resolve();
            }
            // Chốt đoạn đang nói rồi mới đóng, để câu cuối kịp hiện thành chữ.
            ctrl.closing = true;
            flushPcm(true);
            return new Promise(function (resolve) {
                ctrl.commitWaiter = resolve;
                setTimeout(resolve, SCRIBE_COMMIT_WAIT_MS);
            }).then(close);
        };

        const connectTimer = setTimeout(fail, SCRIBE_CONNECT_TIMEOUT_MS);
        fetchScribeToken().then(function (info) {
            if (ctrl.done) return;
            if (!info) {
                scribeUnavailable = true;
                clearTimeout(connectTimer);
                fail();
                return;
            }
            const url = SCRIBE_SOCKET_URL +
                "?model_id=" + encodeURIComponent(info.modelId || "scribe_v2_realtime") +
                "&language_code=" + encodeURIComponent(info.languageCode || (inputLanguage ? inputLanguage.value : "vi")) +
                "&audio_format=pcm_16000&commit_strategy=vad" +
                "&token=" + encodeURIComponent(info.token);
            let ws;
            try {
                ws = new WebSocket(url);
            } catch (_) {
                clearTimeout(connectTimer);
                fail();
                return;
            }
            ctrl.ws = ws;
            ws.onopen = function () {
                clearTimeout(connectTimer);
                ctrl.queue.forEach(function (m) { ws.send(m); });
                ctrl.queue = [];
            };
            ws.onmessage = function (ev) {
                let msg;
                try { msg = JSON.parse(ev.data); } catch (_) { return; }
                switch (msg.message_type) {
                    case "session_started":
                        ctrl.heard = true;
                        return;
                    case "partial_transcript":
                        if (ctrl.done) return;
                        segPartial = msg.text || "";
                        writeLiveText();
                        return;
                    case "committed_transcript":
                    case "committed_transcript_with_timestamps":
                        if (!ctrl.done) {
                            segFinal = joinText(segFinal, msg.text);
                            segPartial = "";
                            writeLiveText();
                        }
                        if (ctrl.closing && ctrl.commitWaiter) ctrl.commitWaiter();
                        return;
                    case "warning":
                    case "insufficient_audio_activity":
                    case "commit_throttled":
                        if (ctrl.closing && ctrl.commitWaiter) ctrl.commitWaiter();
                        return;
                    default:
                        if (msg.error || /error|exceeded|limited|overflow|exhausted|invalid|terms/.test(msg.message_type || "")) {
                            if (ctrl.closing) {
                                if (ctrl.commitWaiter) ctrl.commitWaiter();
                            } else {
                                fail();
                            }
                        }
                }
            };
            ws.onerror = fail;
            ws.onclose = function () {
                clearTimeout(connectTimer);
                if (ctrl.closing) {
                    if (ctrl.commitWaiter) ctrl.commitWaiter();
                    close();
                } else {
                    fail();
                }
            };
        });
        return ctrl;
    }

    // --- Nguồn 2: Web Speech API của trình duyệt (Chrome/Edge) ---
    function startBrowserRecognition() {
        if (!canRecognize || browserSpeechFailed) return false;
        let recognition;
        try {
            recognition = new SpeechRecognitionCtor();
        } catch (_) {
            browserSpeechFailed = true;
            return false;
        }
        const ctrl = { kind: "browser", done: false };
        live = ctrl;
        beginLiveText();

        recognition.lang = getSelectedLang();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.onresult = function (event) {
            if (ctrl.done) return;
            let interim = "";
            for (let i = event.resultIndex; i < event.results.length; i++) {
                const res = event.results[i];
                if (res.isFinal) {
                    segFinal = joinText(segFinal, res[0].transcript);
                } else {
                    interim += res[0].transcript;
                }
            }
            segPartial = interim;
            writeLiveText();
        };
        recognition.onerror = function (ev) {
            const code = ev && ev.error;
            if (ctrl.done || code === "no-speech" || code === "aborted") return;
            browserSpeechFailed = true;
            ctrl.stop();
            if (code === "audio-capture") {
                setNotice("Không nhận được âm thanh từ micro. Hãy kiểm tra micro rồi thử lại, hoặc gõ câu trả lời.", "error");
            } else {
                onLiveFailed(true);
            }
        };
        recognition.onend = function () {
            if (!ctrl.done && isRecording) {
                try { recognition.start(); } catch (_) { }
            }
        };
        ctrl.stop = function () {
            if (!ctrl.done) {
                ctrl.done = true;
                recognition.onend = null;
                try { recognition.stop(); } catch (_) { }
                if (live === ctrl) live = null;
                syncTranscriptLock();
            }
            return Promise.resolve();
        };
        try {
            recognition.start();
        } catch (_) {
            browserSpeechFailed = true;
            ctrl.stop();
            return false;
        }
        syncTranscriptLock();
        return true;
    }

    function startLiveTranscription() {
        if (!scribeUnavailable && pcmSink === null && audioGraph && audioGraph.processor && "WebSocket" in window) {
            startScribe();
        } else if (!startBrowserRecognition()) {
            liveLost = true;
        }
        syncTranscriptLock();
    }

    // Nguồn đang chạy hỏng: chữ đã có trong ô được giữ nguyên, chuyển sang nguồn kế tiếp.
    function onLiveFailed(midAnswer) {
        if (!isRecording) return;
        if (startBrowserRecognition()) {
            if (midAnswer) setNotice("Đã chuyển sang nhận diện giọng nói của trình duyệt. Phần chữ đã có được giữ nguyên.", "info");
            else render();
            return;
        }
        liveLost = true;
        syncTranscriptLock();
        setNotice(recorder
            ? "Không hiện được chữ trực tiếp. Bạn cứ nói tiếp, bản ghi âm vẫn được lưu; khi bạn dừng, AI Server sẽ nhận diện bản ghi âm. Bạn cũng có thể gõ câu trả lời."
            : "Không chuyển giọng nói thành chữ được trên trình duyệt này. Hãy gõ câu trả lời.", "error");
    }

    function updateRecordingClock() {
        if (!recElapsed) return;
        const ms = recordedMs + (isRecording ? Date.now() - segmentStartedAt : 0);
        recElapsed.textContent = formatSeconds(ms / 1000);
    }

    function buildAudioFromChunks() {
        if (!audioChunks.length) return;
        const mimeType = (recorder && recorder.mimeType) || "audio/webm";
        lastAudioBlob = new Blob(audioChunks, { type: mimeType });

        if (inputAudioFile) {
            try {
                const dt = new DataTransfer();
                dt.items.add(new File([lastAudioBlob], "viva-answer." + audioExt, { type: mimeType }));
                inputAudioFile.files = dt.files;
            } catch (_) {
                // Trình duyệt cũ không hỗ trợ DataTransfer constructor: chỉ nộp phần văn bản.
            }
        }
        if (inputAudioDuration) {
            inputAudioDuration.value = String(Math.max(1, Math.round(recordedMs / 1000)));
        }
        if (audioPreviewPlayer && audioPreviewWrap) {
            if (previewUrl) URL.revokeObjectURL(previewUrl);
            previewUrl = URL.createObjectURL(lastAudioBlob);
            audioPreviewPlayer.src = previewUrl;
            audioPreviewWrap.hidden = false;
        }
        if (btnServerStt) btnServerStt.hidden = false;
        syncTranscriptRequired();
    }

    // Lấy phần âm thanh đã ghi tới lúc tạm dừng (requestData) để nghe lại / nhận diện / đính kèm.
    function flushRecorder() {
        return new Promise(function (resolve) {
            const done = function () {
                recorder.removeEventListener("dataavailable", done);
                resolve();
            };
            recorder.addEventListener("dataavailable", done);
            setTimeout(done, 1000);
            try {
                recorder.requestData();
            } catch (_) {
                done();
            }
        });
    }

    async function startVoiceCapture() {
        if (isRecording || isStarting || isSubmitting || studentState === "transcribing") return;
        stopExaminerVoice();
        notice = null;
        ensureAudioContext();

        if (canCapture && !mediaStream) {
            isStarting = true;
            try {
                mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            } catch (err) {
                releaseMicrophone();
                setNotice(micErrorMessage(err), "error");
                return;
            } finally {
                isStarting = false;
            }
            if (isSubmitting) {
                releaseMicrophone();
                return;
            }
            connectAudioGraph(mediaStream);
        }

        if (canRecord && mediaStream && !recorder) {
            const chosen = pickSupportedMimeType();
            audioExt = chosen.ext;
            try {
                recorder = chosen.mime ? new MediaRecorder(mediaStream, { mimeType: chosen.mime }) : new MediaRecorder(mediaStream);
                audioChunks = [];
                recorder.ondataavailable = function (ev) {
                    if (ev.data && ev.data.size > 0) audioChunks.push(ev.data);
                };
                textBeforeRecording = transcriptInput ? transcriptInput.value.trim() : "";
                liveProducedText = false;
                recorder.start(250);
            } catch (_) {
                recorder = null;
            }
        } else if (recorder && recorder.state === "paused") {
            recorder.resume();
        }

        isRecording = true;
        liveLost = false;
        segmentStartedAt = Date.now();
        startLiveTranscription();
        if (!recorder && !live) {
            isRecording = false;
            releaseMicrophone();
            setNotice("Không bắt đầu ghi âm được trên trình duyệt này. Hãy gõ câu trả lời.", "error");
            return;
        }
        if (liveLost && recorder) {
            notice = { text: "Không hiện được chữ trực tiếp trên trình duyệt này. Bạn cứ nói, khi bạn dừng AI Server sẽ nhận diện bản ghi âm. Bạn cũng có thể gõ câu trả lời.", tone: "info" };
        }
        syncTranscriptRequired();
        studentState = "recording";
        if (recIndicator) recIndicator.hidden = false;
        updateRecordingClock();
        runLevelMeter();
        render();
    }

    async function pauseVoiceCapture() {
        if (!isRecording) return;
        isRecording = false;
        recordedMs += Date.now() - segmentStartedAt;
        const lostThisTime = liveLost;
        notice = null;
        studentState = "idle";
        render();

        const liveStop = live ? live.stop(true) : Promise.resolve();
        if (recorder && recorder.state === "recording") {
            recorder.pause();
            await flushRecorder();
        }
        await liveStop;
        if (isRecording || isSubmitting) return;
        buildAudioFromChunks();
        updateRecordingClock();
        render();

        // Chưa có chữ trực tiếp nào cho bản ghi (mất kết nối, hoặc nguồn chữ im lặng): nhờ AI Server nhận diện.
        if (hasAudio() && !liveProducedText) {
            transcribeOnServer();
        } else if (lostThisTime && hasAudio()) {
            setNotice("Một phần câu trả lời chưa hiện thành chữ. Bấm “Nhận diện lại bằng AI Server” để nhận diện toàn bộ bản ghi âm (thay cho phần chữ đã nói), hoặc gõ bổ sung.", "info");
        }
    }

    // Dừng hẳn recorder và chữ trực tiếp trước khi nộp. graceful: chờ Scribe chốt câu cuối (nộp thủ công);
    // khi hết giờ thì dừng ngay để kịp hạn.
    function finalizeRecording(graceful) {
        if (isRecording) {
            isRecording = false;
            recordedMs += Date.now() - segmentStartedAt;
        }
        const liveStop = live ? live.stop(graceful) : Promise.resolve();
        const recorderStop = (!recorder || recorder.state === "inactive")
            ? Promise.resolve()
            : new Promise(function (resolve) {
                let finished = false;
                const finish = function () {
                    if (finished) return;
                    finished = true;
                    buildAudioFromChunks();
                    resolve();
                };
                recorder.addEventListener("stop", finish);
                setTimeout(finish, 1200);
                try {
                    recorder.stop();
                } catch (_) {
                    finish();
                }
            });
        return Promise.all([liveStop, recorderStop]).then(releaseMicrophone);
    }

    window.addEventListener("pagehide", function () {
        if (live) live.stop(false);
        if (recorder && recorder.state !== "inactive") {
            try { recorder.stop(); } catch (_) { }
        }
        releaseMicrophone();
    });

    function renderMicButton() {
        if (!btnToggleMic) return;
        const recording = studentState === "recording";
        btnToggleMic.classList.toggle("btn-danger", recording);
        btnToggleMic.classList.toggle("viva-mic-recording", recording);
        btnToggleMic.classList.toggle("btn-primary", !recording);
        btnToggleMic.setAttribute("aria-pressed", recording ? "true" : "false");
        btnToggleMic.disabled = isSubmitting || studentState === "transcribing";
        if (micBtnLabel) {
            micBtnLabel.textContent = recording
                ? "Dừng ghi âm"
                : (recorder || hasAudio() ? "Nói tiếp" : "Bắt đầu trả lời");
        }
        if (recIndicator) recIndicator.classList.toggle("is-paused", !recording);
    }

    if (voiceBar && !canCapture && !canRecognize) {
        voiceBar.hidden = true;
    }

    if (btnToggleMic) {
        btnToggleMic.addEventListener("click", function () {
            if (isRecording) {
                pauseVoiceCapture();
            } else {
                startVoiceCapture();
            }
        });
    }

    // ---------------- 3. Nhận diện bản ghi âm phía Server ----------------
    async function transcribeOnServer() {
        if (!hasAudio() || isRecording || isSubmitting || studentState === "transcribing") return;

        sttAbort = typeof AbortController !== "undefined" ? new AbortController() : null;
        studentState = "transcribing";
        notice = null;
        render();

        try {
            const formData = new FormData();
            formData.append("audioFile", lastAudioBlob, "viva-answer." + audioExt);
            formData.append("sessionId", sessionId);
            formData.append("language", inputLanguage ? inputLanguage.value : "vi");

            const resp = await fetch(transcribeUrl, {
                method: "POST",
                headers: antiforgeryHeaders(),
                body: formData,
                signal: sttAbort ? sttAbort.signal : undefined
            });
            const data = await resp.json().catch(function () { return null; });
            if (isSubmitting) return;

            studentState = "idle";
            if (resp.ok && data && data.transcript) {
                if (transcriptInput) {
                    transcriptInput.value = joinText(textBeforeRecording, data.transcript);
                }
                setNotice("AI Server đã chuyển bản ghi âm thành văn bản. Hãy kiểm tra lại trước khi nộp.", "info");
            } else {
                setNotice(((data && data.error) || "AI Server chưa nhận diện được bản ghi âm.") +
                    " Bạn có thể gõ câu trả lời, hoặc vẫn nộp kèm bản ghi âm.", "error");
            }
        } catch (_) {
            if (isSubmitting) return;
            studentState = "idle";
            setNotice("Không kết nối được dịch vụ nhận diện giọng nói. Bạn có thể gõ câu trả lời, hoặc vẫn nộp kèm bản ghi âm.", "error");
        } finally {
            sttAbort = null;
        }
    }

    if (btnServerStt) {
        btnServerStt.addEventListener("click", transcribeOnServer);
    }

    // ---------------- 4. Nộp câu trả lời ----------------
    function renderSubmitButton() {
        if (!btnSubmitAnswer) return;
        btnSubmitAnswer.disabled = isSubmitting || studentState === "transcribing";
        if (submitBtnLabel) {
            submitBtnLabel.textContent = isSubmitting ? "Đang nộp…" : "Nộp câu trả lời";
        }
    }

    function beginSubmit(state) {
        isSubmitting = true;
        studentState = state;
        if (sttAbort) sttAbort.abort();
        stopExaminerVoice();
        render();
    }

    function timeIsUp() {
        return Date.now() >= Math.min(turnDeadline, slotDeadline);
    }

    async function submitAnswer() {
        if (isSubmitting || !answerForm) return;
        beginSubmit("submitting");
        await finalizeRecording(true);

        if (timeIsUp()) {
            if (inputTimedOut) inputTimedOut.disabled = false;
            answerForm.submit();
            return;
        }
        if (transcriptInput && !transcriptInput.value.trim() && !hasAudio()) {
            isSubmitting = false;
            studentState = "idle";
            setNotice("Bạn chưa có câu trả lời. Hãy trả lời bằng giọng nói hoặc gõ câu trả lời trước khi nộp.", "error");
            transcriptInput.focus();
            return;
        }
        answerForm.submit();
    }

    // Hết giờ: nộp nguyên trạng (có thể trống) kèm timedOut=true. form.submit() không chạy kiểm tra `required`.
    // Chỉ tự nộp một lần cho mỗi lượt: nếu quay lại đúng lượt đó (server chưa chuyển câu) thì không tự nộp lặp lại.
    async function submitOnTimeout(kind) {
        if (!answerForm || isSubmitting) return;
        const onceKey = "aives_timeout_posted_" + activeTurnId;
        if (sessionStorage.getItem(onceKey)) {
            setNotice("Đã hết thời gian lượt này nhưng hệ thống chưa chuyển sang câu tiếp theo. Bấm “Nộp câu trả lời” để nộp lại; nếu vẫn không được, hãy báo giám thị.", "error");
            return;
        }
        sessionStorage.setItem(onceKey, "1");
        beginSubmit(kind === "slot" ? "timeout-slot" : "timeout-turn");
        await finalizeRecording(false);
        if (inputTimedOut) inputTimedOut.disabled = false;
        answerForm.submit();
    }

    if (answerForm) {
        answerForm.addEventListener("submit", function (ev) {
            ev.preventDefault();
            submitAnswer();
        });
    }

    if (btnSubmitAnswer && transcriptInput) {
        // Chạy trước bước kiểm tra của form, để thông báo cũ không chặn khi đã có bản ghi âm.
        btnSubmitAnswer.addEventListener("click", function () {
            transcriptInput.setCustomValidity("");
        });
    }
    if (transcriptInput) {
        transcriptInput.addEventListener("invalid", function () {
            transcriptInput.setCustomValidity("Hãy trả lời bằng giọng nói hoặc gõ câu trả lời trước khi nộp.");
        });
        transcriptInput.addEventListener("input", function () {
            transcriptInput.setCustomValidity("");
        });
    }

    // ---------------- 5. Đồng hồ đếm ngược ----------------
    // Tính theo mốc thời gian thực để không bị trôi khi tab chạy nền; lượt trả lời không thể dài hơn phần còn lại của ca.
    if (!isCompleted && (turnTimerEl || slotTimerEl)) {
        const turnWarnAt = Math.min(30, Math.floor(turnLimitSeconds / 3));
        const turnDangerAt = Math.min(10, Math.floor(turnLimitSeconds / 6));
        const announced = {};
        let timerHandle = 0;

        const secondsLeft = function (deadline) {
            return Math.max(0, Math.ceil((deadline - Date.now()) / 1000));
        };
        const announce = function (key, text) {
            if (announced[key] || !timerAnnouncer) return;
            announced[key] = true;
            timerAnnouncer.textContent = text;
        };
        const setLevel = function (box, level) {
            if (!box) return;
            box.classList.toggle("is-warning", level === "warning");
            box.classList.toggle("is-danger", level === "danger");
        };

        const tick = function () {
            const slotLeft = secondsLeft(slotDeadline);
            const turnLeft = Math.min(secondsLeft(turnDeadline), slotLeft);

            if (turnTimerEl) {
                turnTimerEl.textContent = formatSeconds(turnLeft);
                const level = turnLeft <= turnDangerAt ? "danger" : (turnLeft <= turnWarnAt ? "warning" : "");
                setLevel(turnTimerBox, level);
                if (turnTimerBar && turnLimitSeconds > 0) {
                    turnTimerBar.style.transform = "scaleX(" + Math.min(1, turnLeft / turnLimitSeconds).toFixed(4) + ")";
                }
                if (activeTurnId && turnLeft > 0) {
                    if (level === "warning") announce("turn-warning", "Còn " + turnLeft + " giây để trả lời lượt này.");
                    if (level === "danger") announce("turn-danger", "Còn " + turnLeft + " giây. Hết giờ hệ thống sẽ tự nộp phần bạn đã có.");
                }
            }
            if (slotTimerEl) {
                slotTimerEl.textContent = formatSeconds(slotLeft);
                const level = slotLeft <= 30 ? "danger" : (slotLeft <= 120 ? "warning" : "");
                setLevel(slotTimerBox, level);
                if (level === "warning" && slotLeft > 0) announce("slot-warning", "Ca thi còn khoảng " + Math.ceil(slotLeft / 60) + " phút.");
            }
            updateRecordingClock();

            if (isSubmitting || !answerForm) return;
            if (slotLeft <= 0) {
                clearInterval(timerHandle);
                submitOnTimeout("slot");
            } else if (turnLeft <= 0) {
                clearInterval(timerHandle);
                submitOnTimeout("turn");
            }
        };

        tick();
        timerHandle = setInterval(tick, 500);
    }

    render();
})();
