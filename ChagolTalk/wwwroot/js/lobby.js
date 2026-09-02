// ==========================================
// MATCHMAKING LOBBY
// One button. Mode/interest/language pickers were removed -- every room
// supports both voice and text regardless, and ChatMode.Any matches with
// everyone, so the options cost a screenful of scrolling without
// meaningfully changing who you got paired with.
// ==========================================

(function () {
    "use strict";

    const root = document.getElementById("lobbyRoot");

    // ---------- DOM ----------

    const micCheck = document.getElementById("micCheck");
    const micStatusText = document.getElementById("micStatusText");

    const startButton = document.getElementById("startButton");
    const idleState = document.getElementById("idleState");
    const searchingState = document.getElementById("searchingState");
    const cancelSearchButton = document.getElementById("cancelSearchButton");
    const searchingSub = document.getElementById("searchingSub");
    const lobbyError = document.getElementById("lobbyError");

    const onlineCountEl = document.getElementById("onlineCount");
    const estimatedWaitEl = document.getElementById("estimatedWait");

    // ---------- SEARCHING SOUND ----------

    const searchingSound = new Audio("/sounds/magiaz-goat-411846.mp3");
    searchingSound.loop = true;

    // The file is mastered at speech level -- measured at -17.4 dBFS over its
    // loudest second, peaking at -2.8 dBFS. That is fine for a one-shot sound
    // effect and much too loud for something that loops every five seconds in
    // your ear while you wait, especially on headphones, which is how most
    // people arrive at a voice-chat site.
    //
    // 0.25 lands the loop near -29 dBFS: still clearly audible, but under
    // speech rather than competing with it. It also leaves headroom for the
    // stranger's voice, which starts the moment this stops.
    searchingSound.volume = 0.25;

    function playSearchingSound() {
        searchingSound.currentTime = 0;
        // Browsers can reject play() if it's not tied closely enough to a
        // user gesture -- not fatal, just no sound that time.
        searchingSound.play().catch((error) => console.error("Searching sound error:", error));
    }

    function stopSearchingSound() {
        searchingSound.pause();
        searchingSound.currentTime = 0;
    }

    // ---------- MIC ----------
    // Asked for up front so the permission prompt doesn't ambush someone
    // mid-call, and so the first call connects without waiting on it.

    let micStream = null;

    async function requestMicAccess() {
        if (micStream) return;

        try {
            micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            micCheck.classList.add("granted");
            micCheck.classList.remove("denied");
            micStatusText.textContent = "Microphone ready";
        } catch (error) {
            console.error("Microphone permission error:", error);
            micCheck.classList.add("denied");
            micCheck.classList.remove("granted");
            micStatusText.textContent = "Microphone blocked — voice calls won't work";
        }
    }

    function stopMicStream() {
        if (micStream) {
            micStream.getTracks().forEach((track) => track.stop());
            micStream = null;
        }
    }

    requestMicAccess();

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

    // Both the page-load connect and the "Start Talking" click can race to
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
        startButton.textContent = "Start Talking";
        stopSearchingSound();
    }

    connection.on("WaitingForMatch", (waitingCount, estimatedWaitSeconds) => {
        searchingSub.textContent = typeof estimatedWaitSeconds === "number"
            ? `Usually takes about ${estimatedWaitSeconds}s...`
            : "We're looking for someone for you...";

        if (estimatedWaitEl && typeof estimatedWaitSeconds === "number") {
            estimatedWaitEl.textContent = estimatedWaitSeconds;
        }
    });

    connection.on("MatchFound", (conversationId) => {
        stopSearchingSound();
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
        playSearchingSound();

        try {
            await ensureConnected();

            enterSearchingUI();

            // "any" pairs with everyone, and the room offers voice and text
            // either way -- see the note at the top of this file.
            await connection.invoke("StartMatching", "any", "", "");
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
    // Set when arriving fresh from the home page's quick-start popup --
    // skips this screen entirely and goes straight to searching, matching
    // what the popup promised.

    if (root.dataset.autoStart === "true") {
        startButton.click();
    }
})();
