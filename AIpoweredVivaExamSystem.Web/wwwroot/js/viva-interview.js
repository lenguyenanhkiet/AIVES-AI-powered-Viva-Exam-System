// Logic phòng thi vấn đáp AI (AIVES Core — Group 3):
// 1. Text-to-Speech (TTS) đọc câu hỏi của Giám khảo AI (vi-VN / en-US).
// 2. Speech-to-Text (STT) thời gian thực qua Web Speech API + Thu âm câu trả lời qua MediaRecorder.
// 3. Fallback nhận diện giọng nói phía Server (/VivaSessions/Transcribe — OpenAI Whisper).
// 4. Đồng hồ đếm ngược thời gian mỗi lượt hỏi và thời gian ca thi, tự động nộp khi hết giờ.
(function () {
    "use strict";

    const root = document.getElementById("viva-interview-root");
    if (!root) return;

    const isCompleted = root.dataset.isCompleted === "true";
    const sessionId = root.dataset.sessionId || "";
    let remainingSlotSeconds = parseInt(root.dataset.remainingSlotSeconds || "0", 10);
    let remainingTurnSeconds = parseInt(root.dataset.remainingTurnSeconds || "0", 10);

    const turnTimerEl = document.getElementById("turn-timer");
    const slotTimerEl = document.getElementById("slot-timer");
    const langSelect = document.getElementById("viva-lang-select");
    const inputLanguage = document.getElementById("input-language");
    const btnSpeakActive = document.getElementById("btn-speak-active");
    const btnToggleMic = document.getElementById("btn-toggle-mic");
    const micBtnLabel = document.getElementById("mic-btn-label");
    const micStatusText = document.getElementById("mic-status-text");
    const btnServerStt = document.getElementById("btn-server-stt");
    const audioPreviewWrap = document.getElementById("audio-preview-wrap");
    const audioPreviewPlayer = document.getElementById("audio-preview-player");
    const inputAudioFile = document.getElementById("input-audio-file");
    const inputAudioDuration = document.getElementById("input-audio-duration");
    const transcriptInput = document.getElementById("transcript-input");
    const answerForm = document.getElementById("viva-answer-form");
    const btnSubmitAnswer = document.getElementById("btn-submit-answer");
    const submitBtnLabel = document.getElementById("submit-btn-label");

    let isSubmitting = false;
    let isRecording = false;
    let recognition = null;
    let mediaRecorder = null;
    let mediaStream = null;
    let audioChunks = [];
    let recordStartTime = 0;
    let lastAudioBlob = null;
    let lastAudioExt = "webm";
    let baseTranscriptBeforeMic = "";
    let finalizedSegment = "";

    function formatSeconds(totalSeconds) {
        const safe = Math.max(0, Math.floor(totalSeconds));
        const mins = Math.floor(safe / 60);
        const secs = safe % 60;
        return String(mins).padStart(2, "0") + ":" + String(secs).padStart(2, "0");
    }

    function getSelectedLang() {
        return langSelect ? langSelect.value : "vi-VN";
    }

    function syncShortLanguageInput() {
        if (!inputLanguage) return;
        const lang = getSelectedLang();
        inputLanguage.value = lang.toLowerCase().startsWith("en") ? "en" : "vi";
    }

    if (langSelect) {
        const savedLang = sessionStorage.getItem("aives_viva_lang");
        if (savedLang && (savedLang === "vi-VN" || savedLang === "en-US")) {
            langSelect.value = savedLang;
        }
        syncShortLanguageInput();
        langSelect.addEventListener("change", function () {
            sessionStorage.setItem("aives_viva_lang", langSelect.value);
            syncShortLanguageInput();
        });
    }

    // ---------------- 1. Text-to-Speech (TTS) ----------------
    function speakText(text) {
        if (!("speechSynthesis" in window) || !text) return;

        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text.trim());
        const targetLang = getSelectedLang();
        utterance.lang = targetLang;
        utterance.rate = 1.0;

        const voices = window.speechSynthesis.getVoices();
        const langPrefix = targetLang.split("-")[0].toLowerCase();
        const matchedVoice =
            voices.find(v => v.lang && v.lang.toLowerCase() === targetLang.toLowerCase()) ||
            voices.find(v => v.lang && v.lang.toLowerCase().startsWith(langPrefix));
        if (matchedVoice) {
            utterance.voice = matchedVoice;
        }

        window.speechSynthesis.speak(utterance);
    }

    if (btnSpeakActive) {
        btnSpeakActive.addEventListener("click", function () {
            const promptText = btnSpeakActive.getAttribute("data-tts-text") || "";
            speakText(promptText);
        });
    }

    document.querySelectorAll(".js-tts-replay").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const text = btn.getAttribute("data-tts-text") || "";
            speakText(text);
        });
    });

    // Tự động đọc câu hỏi mới của Giám khảo AI khi vào lượt trả lời
    if (!isCompleted && btnSpeakActive) {
        const activeText = btnSpeakActive.getAttribute("data-tts-text") || "";
        const currentTurnId = root.dataset.turnId || "";
        const lastSpokenTurnId = sessionStorage.getItem("aives_last_spoken_turn");
        if (activeText && currentTurnId && lastSpokenTurnId !== currentTurnId) {
            sessionStorage.setItem("aives_last_spoken_turn", currentTurnId);
            setTimeout(function () {
                speakText(activeText);
            }, 350);
        }
    }

    // ---------------- 2. Speech-to-Text (STT) & MediaRecorder ----------------
    function pickSupportedMimeType() {
        if (typeof MediaRecorder === "undefined") return { mime: "", ext: "webm" };
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

    function attachBlobToFileInput(blob, ext) {
        if (!inputAudioFile || !blob || blob.size === 0) return;
        try {
            const fileName = "viva-answer." + (ext || "webm");
            const file = new File([blob], fileName, { type: blob.type || "audio/webm" });
            const dt = new DataTransfer();
            dt.items.add(file);
            inputAudioFile.files = dt.files;
        } catch (_) {
            // Trình duyệt cũ không hỗ trợ DataTransfer constructor
        }
    }

    async function startVoiceCapture() {
        if (isRecording || isSubmitting) return;

        if ("speechSynthesis" in window) {
            window.speechSynthesis.cancel();
        }

        baseTranscriptBeforeMic = transcriptInput ? transcriptInput.value.trim() : "";
        finalizedSegment = "";
        audioChunks = [];
        recordStartTime = Date.now();

        // Khởi tạo MediaRecorder để lưu bằng chứng ghi âm & hỗ trợ Whisper STT
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            try {
                mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
                const chosen = pickSupportedMimeType();
                lastAudioExt = chosen.ext;
                mediaRecorder = chosen.mime
                    ? new MediaRecorder(mediaStream, { mimeType: chosen.mime })
                    : new MediaRecorder(mediaStream);

                mediaRecorder.ondataavailable = function (ev) {
                    if (ev.data && ev.data.size > 0) {
                        audioChunks.push(ev.data);
                    }
                };

                mediaRecorder.onstop = function () {
                    const durationSec = Math.max(1, Math.round((Date.now() - recordStartTime) / 1000));
                    if (inputAudioDuration) {
                        inputAudioDuration.value = String(durationSec);
                    }

                    if (audioChunks.length > 0) {
                        const mimeType = (mediaRecorder && mediaRecorder.mimeType) || "audio/webm";
                        lastAudioBlob = new Blob(audioChunks, { type: mimeType });
                        attachBlobToFileInput(lastAudioBlob, lastAudioExt);

                        if (audioPreviewPlayer && audioPreviewWrap) {
                            audioPreviewPlayer.src = URL.createObjectURL(lastAudioBlob);
                            audioPreviewWrap.hidden = false;
                        }
                        if (btnServerStt) {
                            btnServerStt.hidden = false;
                        }
                    }

                    if (mediaStream) {
                        mediaStream.getTracks().forEach(function (t) { t.stop(); });
                        mediaStream = null;
                    }
                };

                mediaRecorder.start(250);
            } catch (err) {
                if (micStatusText) {
                    micStatusText.textContent = "Không thể truy cập Microphone để ghi âm (" + (err.message || "Permission denied") + "). Bạn vẫn có thể nhập câu trả lời.";
                }
            }
        }

        // Khởi tạo Web Speech API (SpeechRecognition) để chuyển giọng nói thành văn bản realtime
        const SpeechRecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (SpeechRecognitionCtor) {
            try {
                recognition = new SpeechRecognitionCtor();
                recognition.lang = getSelectedLang();
                recognition.continuous = true;
                recognition.interimResults = true;

                recognition.onresult = function (event) {
                    let interimSegment = "";
                    for (let i = event.resultIndex; i < event.results.length; i++) {
                        const res = event.results[i];
                        if (res.isFinal) {
                            finalizedSegment += res[0].transcript + " ";
                        } else {
                            interimSegment += res[0].transcript;
                        }
                    }

                    if (transcriptInput) {
                        const combinedSpoken = (finalizedSegment + interimSegment).trim();
                        transcriptInput.value = baseTranscriptBeforeMic
                            ? (baseTranscriptBeforeMic + " " + combinedSpoken).trim()
                            : combinedSpoken;
                    }
                };

                recognition.onerror = function () {
                    // Nếu Web Speech gặp lỗi mạng/trình duyệt, người dùng vẫn có file ghi âm & nút Whisper Server STT
                };

                recognition.onend = function () {
                    if (isRecording && recognition) {
                        try {
                            recognition.start();
                        } catch (_) { }
                    }
                };

                recognition.start();
            } catch (_) {
                recognition = null;
            }
        }

        isRecording = true;
        if (btnToggleMic) {
            btnToggleMic.classList.remove("btn-primary");
            btnToggleMic.classList.add("btn-danger", "viva-mic-recording");
        }
        if (micBtnLabel) {
            micBtnLabel.textContent = "Dừng ghi âm (Đang nghe...)";
        }
        if (micStatusText) {
            micStatusText.textContent = SpeechRecognitionCtor
                ? "Đang thu âm và chuyển giọng nói sang văn bản theo thời gian thực..."
                : "Đang thu âm giọng nói (trình duyệt không hỗ trợ Web Speech, sẽ tự động nhận diện qua AI Server)...";
        }
    }

    function stopVoiceCapture() {
        return new Promise(function (resolve) {
            if (!isRecording) {
                resolve();
                return;
            }

            isRecording = false;

            if (recognition) {
                try {
                    recognition.onend = null;
                    recognition.stop();
                } catch (_) { }
                recognition = null;
            }

            if (btnToggleMic) {
                btnToggleMic.classList.remove("btn-danger", "viva-mic-recording");
                btnToggleMic.classList.add("btn-primary");
            }
            if (micBtnLabel) {
                micBtnLabel.textContent = "Tiếp tục nói thêm bằng giọng nói (Mic)";
            }
            if (micStatusText) {
                micStatusText.textContent = "Đã dừng thu âm. Bạn có thể rà soát lại thuật ngữ chuyên ngành trong ô văn bản trước khi nộp.";
            }

            if (mediaRecorder && mediaRecorder.state !== "inactive") {
                const prevOnStop = mediaRecorder.onstop;
                mediaRecorder.onstop = function (ev) {
                    if (typeof prevOnStop === "function") {
                        prevOnStop.call(mediaRecorder, ev);
                    }
                    resolve();
                };
                try {
                    mediaRecorder.stop();
                } catch (_) {
                    resolve();
                }
            } else {
                resolve();
            }
        });
    }

    if (btnToggleMic) {
        btnToggleMic.addEventListener("click", function () {
            if (isRecording) {
                stopVoiceCapture();
            } else {
                startVoiceCapture();
            }
        });
    }

    // ---------------- 3. Server-side Whisper STT Fallback ----------------
    if (btnServerStt) {
        btnServerStt.addEventListener("click", async function () {
            if (!lastAudioBlob || lastAudioBlob.size === 0) return;

            btnServerStt.disabled = true;
            const originalText = btnServerStt.textContent;
            btnServerStt.textContent = "Đang nhận diện bằng AI Server...";

            try {
                const formData = new FormData();
                const fileName = "viva-answer." + (lastAudioExt || "webm");
                formData.append("audioFile", lastAudioBlob, fileName);
                formData.append("sessionId", sessionId);
                formData.append("language", inputLanguage ? inputLanguage.value : "vi");

                const tokenInput = answerForm ? answerForm.querySelector('input[name="__RequestVerificationToken"]') : null;
                const headers = { "X-Requested-With": "XMLHttpRequest" };
                if (tokenInput && tokenInput.value) {
                    headers["RequestVerificationToken"] = tokenInput.value;
                }

                const resp = await fetch("/VivaSessions/Transcribe", {
                    method: "POST",
                    headers: headers,
                    body: formData
                });

                const data = await resp.json();
                if (resp.ok && data && data.transcript) {
                    if (transcriptInput) {
                        transcriptInput.value = data.transcript;
                    }
                    if (micStatusText) {
                        micStatusText.textContent = "Đã nhận diện văn bản từ bản ghi âm bằng AI Server. Bạn có thể kiểm tra lại trước khi nộp.";
                    }
                } else if (micStatusText) {
                    micStatusText.textContent = (data && data.error) || "AI Server chưa nhận diện được âm thanh từ bản thu.";
                }
            } catch (_) {
                if (micStatusText) {
                    micStatusText.textContent = "Lỗi kết nối tới dịch vụ nhận diện giọng nói phía Server.";
                }
            } finally {
                btnServerStt.disabled = false;
                btnServerStt.textContent = originalText;
            }
        });
    }

    // ---------------- 4. Đồng hồ đếm ngược & Tự động nộp khi hết giờ ----------------
    async function triggerAutoSubmitOnTimeout(reasonText) {
        if (isSubmitting || !answerForm) return;
        isSubmitting = true;

        await stopVoiceCapture();

        if (transcriptInput && !transcriptInput.value.trim()) {
            transcriptInput.value = reasonText;
        }

        if (btnSubmitAnswer) {
            btnSubmitAnswer.disabled = true;
        }
        if (submitBtnLabel) {
            submitBtnLabel.textContent = "Hết giờ — Đang tự động nộp câu trả lời...";
        }

        answerForm.submit();
    }

    if (!isCompleted && (turnTimerEl || slotTimerEl)) {
        const updateTimersUi = function () {
            if (turnTimerEl) {
                turnTimerEl.textContent = formatSeconds(remainingTurnSeconds);
                turnTimerEl.classList.toggle("viva-timer-urgent", remainingTurnSeconds <= 15);
            }
            if (slotTimerEl) {
                slotTimerEl.textContent = formatSeconds(remainingSlotSeconds);
                slotTimerEl.classList.toggle("viva-timer-urgent", remainingSlotSeconds <= 30);
            }
        };

        updateTimersUi();

        const timerInterval = setInterval(function () {
            if (isSubmitting) {
                clearInterval(timerInterval);
                return;
            }

            remainingTurnSeconds = Math.max(0, remainingTurnSeconds - 1);
            remainingSlotSeconds = Math.max(0, remainingSlotSeconds - 1);
            updateTimersUi();

            if (remainingSlotSeconds <= 0) {
                clearInterval(timerInterval);
                triggerAutoSubmitOnTimeout("[Hết thời gian ca thi — Hệ thống tự động thu bài]");
            } else if (remainingTurnSeconds <= 0) {
                clearInterval(timerInterval);
                triggerAutoSubmitOnTimeout("[Hết thời gian lượt trả lời — Hệ thống tự động chuyển câu]");
            }
        }, 1000);
    }

    // ---------------- 5. Xử lý khi Sinh viên bấm Nộp câu trả lời ----------------
    if (answerForm) {
        answerForm.addEventListener("submit", async function (ev) {
            if (isSubmitting) return;

            if (isRecording) {
                ev.preventDefault();
                isSubmitting = true;
                if (btnSubmitAnswer) btnSubmitAnswer.disabled = true;
                if (submitBtnLabel) submitBtnLabel.textContent = "Đang đóng gói âm thanh & phân tích...";

                await stopVoiceCapture();

                if (transcriptInput && !transcriptInput.value.trim() && (!inputAudioFile || !inputAudioFile.files.length)) {
                    isSubmitting = false;
                    if (btnSubmitAnswer) btnSubmitAnswer.disabled = false;
                    if (submitBtnLabel) submitBtnLabel.textContent = "Nộp câu trả lời & Tiếp tục";
                    return;
                }

                answerForm.submit();
                return;
            }

            isSubmitting = true;
            if ("speechSynthesis" in window) {
                window.speechSynthesis.cancel();
            }
            if (btnSubmitAnswer) {
                btnSubmitAnswer.disabled = true;
            }
            if (submitBtnLabel) {
                submitBtnLabel.textContent = "Giám khảo AI đang phân tích câu trả lời...";
            }
        });
    }
})();

