// ==========================================
// MATCHMAKING LOBBY
// ==========================================

(function () {
    "use strict";

    const root = document.getElementById("lobbyRoot");

    // ---------- DOM ----------

    const modeOptions = Array.from(document.querySelectorAll(".mode-option"));
    const interestInput = document.getElementById("interestInput");
    const addInterestButton = document.getElementById("addInterestButton");
    const interestChips = document.getElementById("interestChips");
    const languageInput = document.getElementById("languageInput");

    const micCheck = document.getElementById("micCheck");
    const micStatusText = document.getElementById("micStatusText");
    const micLevelFill = document.getElementById("micLevelFill");

    const startButton = document.getElementById("startButton");
    const idleState = document.getElementById("idleState");
    const searchingState = document.getElementById("searchingState");
    const cancelSearchButton = document.getElementById("cancelSearchButton");
    const searchingSub = document.getElementById("searchingSub");
    const lobbyError = document.getElementById("lobbyError");

    const onlineCountEl = document.getElementById("onlineCount");
    const estimatedWaitEl = document.getElementById("estimatedWait");

    // ---------- STATE ----------

    let selectedMode = root.dataset.preferredMode ? root.dataset.preferredMode.toLowerCase() : "voice";
    const interests = new Set(
        (root.dataset.interests || "")
            .split(",")
            .map((i) => i.trim())
            .filter(Boolean)
    );

    function renderMode() {
        modeOptions.forEach((el) => el.classList.toggle("selected", el.dataset.mode === selectedMode));
    }

    modeOptions.forEach((el) => {
        el.addEventListener("click", () => {
            selectedMode = el.dataset.mode;
            renderMode();
            if (selectedMode !== "text") requestMicAccess();
        });
    });

    renderMode();

    function renderChips() {
        interestChips.innerHTML = "";

        interests.forEach((tag) => {
            const chip = document.createElement("span");
            chip.className = "interest-chip";
            chip.innerHTML = "";

            const label = document.createElement("span");
            label.textContent = tag;

            const remove = document.createElement("button");
            remove.type = "button";
            remove.textContent = "✕";
            remove.addEventListener("click", () => {
                interests.delete(tag);
                renderChips();
            });

            chip.appendChild(label);
            chip.appendChild(remove);
            interestChips.appendChild(chip);
        });
    }

    function addInterest() {
        const raw = interestInput.value.trim().toLowerCase();
        if (!raw) return;

        raw.split(",").forEach((part) => {
            const tag = part.trim();
            if (tag && interests.size < 10) interests.add(tag);
        });

        interestInput.value = "";
        renderChips();
    }

    addInterestButton.addEventListener("click", addInterest);

    interestInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === ",") {
            event.preventDefault();
            addInterest();
        }
    });

    renderChips();

    // ---------- MIC CHECK ----------

    let micStream = null;
    let micGranted = false;
    let audioContext = null;
    let analyser = null;
    let micLevelRaf = null;

    async function requestMicAccess() {
        if (micGranted || micStream) return;

        micStatusText.textContent = "Requesting access...";

        try {
            micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            micGranted = true;
            micCheck.classList.add("granted");
            micCheck.classList.remove("denied");
            micStatusText.textContent = "Microphone ready";

            startMicLevelMeter();
        } catch (error) {
            console.error("Microphone permission error:", error);
            micGranted = false;
            micCheck.classList.add("denied");
            micStatusText.textContent = "Microphone blocked - voice calls won't work";
        }
    }

    function startMicLevelMeter() {
        try {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const source = audioContext.createMediaStreamSource(micStream);
            analyser = audioContext.createAnalyser();
            analyser.fftSize = 256;
            source.connect(analyser);

            const data = new Uint8Array(analyser.frequencyBinCount);

            function tick() {
                analyser.getByteFrequencyData(data);
                const avg = data.reduce((a, b) => a + b, 0) / data.length;
                micLevelFill.style.width = Math.min(100, (avg / 90) * 100) + "%";
                micLevelRaf = requestAnimationFrame(tick);
            }

            tick();
        } catch (error) {
            console.error("Mic level meter error:", error);
        }
    }

    function stopMicStream() {
        if (micLevelRaf) cancelAnimationFrame(micLevelRaf);
        if (audioContext) {
            audioContext.close().catch(() => {});
            audioContext = null;
        }
        if (micStream) {
            micStream.getTracks().forEach((t) => t.stop());
            micStream = null;
        }
    }

    if (selectedMode !== "text") requestMicAccess();

    // ---------- SIGNALR ----------

    // See chat.js for why this isn't the default retry policy -- free-tier
    // hosting can take 50s+ to wake up, longer than SignalR's default
    // ~42s retry window.
    class ResilientRetryPolicy {
        nextRetryDelayInMilliseconds(retryContext) {
            if (retryContext.elapsedMilliseconds > 5 * 60 * 1000) return null;

            const delays = [0, 2000, 5000, 10000];
            return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
        }
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect(new ResilientRetryPolicy())
        .build();

    // Both the page-load connect and the "Start Chatting" click can race to
    // call .start() -- SignalR throws if you call it while already
    // connecting. Sharing one in-flight promise means whichever caller gets
    // there first does the actual connecting and everyone else just waits
    // on the same result.
    let connectPromise = null;

    function ensureConnected() {
        if (connection.state === signalR.HubConnectionState.Connected) {
            return Promise.resolve();
        }

        if (!connectPromise) {
            connectPromise = connection.start().catch((error) => {
                connectPromise = null;
                throw error;
            });
        }

        return connectPromise;
    }

    ensureConnected().catch((error) => console.error("SignalR connect error:", error));

    connection.onclose(() => {
        connectPromise = null;
        setTimeout(() => ensureConnected().catch((error) => console.error("SignalR connect error:", error)), 5000);
    });

    connection.on("OnlineCount", (count) => {
        if (onlineCountEl) onlineCountEl.textContent = count;
    });

    function showError(message) {
        lobbyError.textContent = message;
        lobbyError.classList.add("show");
    }

    function hideError() {
        lobbyError.classList.remove("show");
    }

    function enterSearchingUI() {
        idleState.classList.add("hidden");
        searchingState.classList.add("show");
    }

    function exitSearchingUI() {
        idleState.classList.remove("hidden");
        searchingState.classList.remove("show");
        startButton.disabled = false;
        startButton.textContent = "Start Chatting";
    }

    connection.on("WaitingForMatch", (waitingCount, estimatedWaitSeconds) => {
        searchingSub.textContent = typeof estimatedWaitSeconds === "number"
            ? `Usually takes about ${estimatedWaitSeconds}s...`
            : "We're looking for someone for you...";

        if (estimatedWaitEl && typeof estimatedWaitSeconds === "number") {
            estimatedWaitEl.textContent = estimatedWaitSeconds;
        }
    });

    connection.on("MatchFound", (conversationId, partnerName, sharedInterests, mode) => {
        try {
            sessionStorage.setItem("ct_mode", selectedMode);
            sessionStorage.setItem("ct_interests", Array.from(interests).join(","));
            sessionStorage.setItem("ct_language", languageInput.value.trim());
        } catch (e) {
            /* sessionStorage unavailable — "find another" from the room will just use defaults */
        }

        stopMicStream();
        window.location.href = "/Chat/Room?id=" + conversationId;
    });

    connection.on("MatchingBlocked", (reason) => {
        exitSearchingUI();
        showError(reason);
    });

    connection.on("MatchingTimedOut", () => {
        exitSearchingUI();
        showError("Nobody was available right now. Try again in a moment.");
    });

    startButton.addEventListener("click", async () => {
        hideError();
        startButton.disabled = true;
        startButton.textContent = "Connecting...";

        try {
            await ensureConnected();

            enterSearchingUI();

            await connection.invoke(
                "StartMatching",
                selectedMode,
                Array.from(interests).join(","),
                languageInput.value.trim()
            );
        } catch (error) {
            console.error("Start matching error:", error);
            exitSearchingUI();
            showError("Could not connect. Please try again.");
        }
    });

    cancelSearchButton.addEventListener("click", async () => {
        try {
            await connection.invoke("CancelMatching");
        } catch (error) {
            console.error("Cancel matching error:", error);
        }

        exitSearchingUI();
    });

    // ---------- AUTO-START ----------
    // Set when arriving fresh from the home page's "just start talking"
    // quick flow -- skips the idle screen and goes straight to searching
    // with the default voice mode, matching what the popup promised.

    if (root.dataset.autoStart === "true") {
        startButton.click();
    }
})();
