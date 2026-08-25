// ==========================================
// CHAT ROOM
// Handles SignalR text chat + WebRTC voice signalling for /Chat/Room.
// Configuration is read from data-* attributes on #chatRoot so this file
// stays a plain static asset with no server-side templating inside it.
// ==========================================

(function () {
    "use strict";

    const root = document.getElementById("chatRoot");
    const conversationId = root.dataset.conversationId;
    const requestToken = root.dataset.requestToken;

    // ---------- DOM ----------

    const connectionStatus = document.getElementById("connectionStatus");
    const statusDot = document.getElementById("statusDot");
    const strangerName = document.getElementById("strangerName");
    const reconnectBanner = document.getElementById("reconnectBanner");

    const messages = document.getElementById("messages");
    const systemMessage = document.getElementById("systemMessage");
    const emptyHint = document.getElementById("emptyHint");
    const typingIndicator = document.getElementById("typingIndicator");

    const messageInput = document.getElementById("messageInput");
    const sendButton = document.getElementById("sendButton");
    const composerWrap = document.getElementById("composerWrap");

    const endButton = document.getElementById("endButton");
    const skipButton = document.getElementById("skipButton");
    const reportButton = document.getElementById("reportButton");
    const qualityIndicator = document.getElementById("qualityIndicator");

    const endedScreen = document.getElementById("endedScreen");
    const endedIcon = document.getElementById("endedIcon");
    const endedTitle = document.getElementById("endedTitle");
    const endedMessage = document.getElementById("endedMessage");
    const findAnotherButton = document.getElementById("findAnotherButton");
    const reportFromEndedButton = document.getElementById("reportFromEndedButton");
    const cancelSkipButton = document.getElementById("cancelSkipButton");

    const callLaunchArea = document.getElementById("callLaunchArea");
    const startCallButton = document.getElementById("startCallButton");
    const callScreen = document.getElementById("callScreen");
    const callContent = document.getElementById("callContent");
    const callName = document.getElementById("callName");
    const endCallButton = document.getElementById("endCallButton");
    const callStatus = document.getElementById("callStatus");
    const muteCallButton = document.getElementById("muteCallButton");
    const incomingCallScreen = document.getElementById("incomingCallScreen");
    const incomingCallName = document.getElementById("incomingCallName");
    const acceptCallButton = document.getElementById("acceptCallButton");
    const declineCallButton = document.getElementById("declineCallButton");
    const callTimer = document.getElementById("callTimer");
    const callEndedState = document.getElementById("callEndedState");
    const callEndedMessage = document.getElementById("callEndedMessage");
    const closeCallButton = document.getElementById("closeCallButton");
    const minimizeCallButton = document.getElementById("minimizeCallButton");

    const callBar = document.getElementById("callBar");
    const callBarName = document.getElementById("callBarName");
    const callBarTimer = document.getElementById("callBarTimer");
    const callBarMuteButton = document.getElementById("callBarMuteButton");
    const callBarExpandButton = document.getElementById("callBarExpandButton");
    const callBarEndButton = document.getElementById("callBarEndButton");

    const reportModal = document.getElementById("reportModal");
    const reportReasonButtons = Array.from(document.querySelectorAll(".report-reason"));
    const reportDetails = document.getElementById("reportDetails");
    const cancelReportButton = document.getElementById("cancelReportButton");
    const confirmReportButton = document.getElementById("confirmReportButton");

    // ---------- SEARCHING SOUND ----------
    // Same file as the lobby -- plays while re-queuing via Skip/Find Another,
    // stops the moment a match lands.

    const searchingSound = new Audio("/sounds/magiaz-goat-411846.mp3");
    searchingSound.loop = true;

    function playSearchingSound() {
        searchingSound.currentTime = 0;
        searchingSound.play().catch((error) => console.error("Searching sound error:", error));
    }

    function stopSearchingSound() {
        searchingSound.pause();
        searchingSound.currentTime = 0;
    }

    // ---------- SIGNALR ----------

    // Free-tier hosting (Render et al.) can take 50s+ to wake a sleeping
    // instance or recover from a restart. SignalR's default retry policy
    // gives up after ~42s (0s, 2s, 10s, 30s), which is shorter than that --
    // so mid-call users would get stuck on a stale "disconnected" banner
    // even though the server comes back seconds later. This keeps retrying
    // for several minutes instead, quick at first then settling at 10s.
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

    function setStatus(text, tone) {
        connectionStatus.textContent = text;
        statusDot.className = "status-dot" + (tone ? " " + tone : "");
    }

    async function joinConversation() {
        try {
            await connection.invoke("JoinConversation", conversationId);
        } catch (error) {
            console.error("Join conversation error:", error);
            setStatus("Unable to join", "danger");

            if (error && error.message && error.message.toLowerCase().includes("already ended")) {
                showEndedScreen("This conversation has already ended.");
            }
        }
    }

    async function startConnection() {
        // Guards against onclose's fallback retry firing while a reconnect
        // (or another startConnection call) is already in flight -- calling
        // .start() on a non-Disconnected connection throws.
        if (connection.state !== signalR.HubConnectionState.Disconnected) return;

        try {
            setStatus("Connecting...", "warn");
            await connection.start();
            await joinConversation();
        } catch (error) {
            console.error("SignalR connection error:", error);
            setStatus("Connection failed", "danger");
            setTimeout(startConnection, 4000);
        }
    }

    connection.onreconnecting(() => setStatus("Reconnecting...", "warn"));

    connection.onreconnected(async () => {
        setStatus("Connected", null);
        await joinConversation();
    });

    connection.onclose(() => {
        setStatus("Disconnected", "danger");

        // withAutomaticReconnect gives up once ResilientRetryPolicy returns
        // null; onclose fires at that point too, so this is the fallback
        // that keeps trying rather than leaving the user stuck until they
        // manually refresh.
        setTimeout(startConnection, 5000);
    });

    connection.on("JoinedConversation", (id, partnerName, mode, sharedInterests) => {
        setStatus("Connected", null);
        strangerName.textContent = partnerName || "Stranger";
        callName.textContent = partnerName || "Stranger";
        incomingCallName.textContent = partnerName || "Stranger";

        systemMessage.textContent = sharedInterests
            ? `You're connected. You both like ${sharedInterests.split(",").join(", ")}.`
            : "You're connected. Say hello!";
    });

    connection.on("StrangerDisconnected", () => {
        reconnectBanner.classList.add("show");
        setStatus("Stranger disconnected", "warn");
    });

    connection.on("StrangerReconnected", () => {
        reconnectBanner.classList.remove("show");
        setStatus("Connected", null);
    });

    // ---------- TEXT MESSAGING ----------

    function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function addMessage(user, message, isMine) {
        if (emptyHint) emptyHint.remove();

        const element = document.createElement("div");
        element.className = "message " + (isMine ? "me" : "stranger");
        element.innerHTML =
            '<span class="message-author">' + (isMine ? "You" : escapeHtml(user)) + "</span>" +
            escapeHtml(message);

        messages.insertBefore(element, typingIndicator);
        messages.scrollTop = messages.scrollHeight;
    }

    connection.on("ReceiveMessage", (user, message) => {
        hideTyping();
        addMessage(user, message, false);
    });

    connection.on("ReceiveOwnMessage", (user, message) => addMessage(user, message, true));

    async function sendMessage() {
        const message = messageInput.value.trim();
        if (!message) return;

        if (connection.state !== signalR.HubConnectionState.Connected) return;

        try {
            await connection.invoke("SendMessage", conversationId, message);
            messageInput.value = "";
            messageInput.focus();
            sendTyping(false);
        } catch (error) {
            console.error("Send message error:", error);
        }
    }

    sendButton.addEventListener("click", sendMessage);

    messageInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            event.preventDefault();
            sendMessage();
        }
    });

    // ---------- TYPING INDICATOR ----------

    let typingState = false;
    let typingStopTimer = null;

    function sendTyping(isTyping) {
        if (typingState === isTyping) return;
        typingState = isTyping;

        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("Typing", conversationId, isTyping).catch(() => {});
        }
    }

    messageInput.addEventListener("input", () => {
        sendTyping(true);
        clearTimeout(typingStopTimer);
        typingStopTimer = setTimeout(() => sendTyping(false), 1500);
    });

    function hideTyping() {
        typingIndicator.classList.remove("show");
    }

    connection.on("StrangerTyping", (isTyping) => {
        typingIndicator.classList.toggle("show", isTyping);
        if (isTyping) messages.scrollTop = messages.scrollHeight;
    });

    // ---------- ENDING ----------

    let conversationEnded = false;

    function showEndedScreen(message) {
        conversationEnded = true;
        messages.style.display = "none";
        composerWrap.style.display = "none";
        endButton.style.display = "none";
        skipButton.style.display = "none";
        if (reportButton) reportButton.style.display = "none";
        callLaunchArea.style.display = "none";

        endedScreen.style.display = "flex";
        endedMessage.textContent = message;
        setStatus("Conversation ended", "danger");
        reconnectBanner.classList.remove("show");

        cleanupVoiceCall();
        callScreen.style.display = "none";
    }

    connection.on("ConversationEnded", () => showEndedScreen("Your stranger left the conversation."));

    endButton.addEventListener("click", async () => {
        if (!confirm("End this conversation?")) return;

        try {
            await connection.invoke("EndConversation", conversationId);
            showEndedScreen("You ended the conversation.");
        } catch (error) {
            console.error("End conversation error:", error);
            alert("Could not end the conversation.");
        }
    });

    connection.on("MatchFound", (newConversationId) => {
        stopSearchingSound();
        window.location.href = "/Chat/Room?id=" + newConversationId;
    });

    connection.on("WaitingForMatch", (waitingCount, estimatedWaitSeconds) => {
        findAnotherButton.textContent =
            typeof estimatedWaitSeconds === "number"
                ? `Finding someone... (~${estimatedWaitSeconds}s)`
                : "Finding someone...";
    });

    function resetFindingUI() {
        findAnotherButton.disabled = false;
        findAnotherButton.textContent = "Find Someone";
        findAnotherButton.classList.remove("finding");
        endedIcon.classList.remove("searching");
        endedIcon.textContent = "👋";
        cancelSkipButton.style.display = "none";
        reportFromEndedButton.style.display = "";
        stopSearchingSound();
    }

    connection.on("MatchingBlocked", (reason) => {
        resetFindingUI();
        endedTitle.textContent = "Conversation ended";
        endedMessage.textContent = reason;
    });

    connection.on("MatchingTimedOut", () => {
        resetFindingUI();
        endedTitle.textContent = "Conversation ended";
        endedMessage.textContent = "Nobody was available just now. Try again?";
    });

    function enterFindingUI() {
        findAnotherButton.disabled = true;
        findAnotherButton.textContent = "Finding someone...";
        findAnotherButton.classList.add("finding");
    }

    findAnotherButton.addEventListener("click", async () => {
        try {
            playSearchingSound();
            enterFindingUI();

            const prefs = readStoredPreferences();
            await connection.invoke("FindAnother", prefs.mode, prefs.interests, prefs.language);
        } catch (error) {
            console.error("Find another error:", error);
            resetFindingUI();
        }
    });

    // ---------- SKIP ----------
    // One click that ends the current conversation AND immediately starts
    // looking for the next stranger, instead of making the user end, look
    // at a static "ended" screen, then click Find Someone separately.

    skipButton.addEventListener("click", async () => {
        try {
            skipButton.disabled = true;
            playSearchingSound();

            if (peerConnection) {
                try {
                    await connection.invoke("EndVoiceCall", conversationId, currentCallDurationSeconds());
                } catch (error) {
                    console.error("End voice call before skip error:", error);
                }
                cleanupVoiceCall();
            }

            showEndedScreen("Looking for someone new for you...");
            endedIcon.classList.add("searching");
            endedIcon.textContent = "🔄";
            endedTitle.textContent = "Finding someone new...";
            reportFromEndedButton.style.display = "none";
            cancelSkipButton.style.display = "";
            setStatus("Searching...", "warn");

            enterFindingUI();

            const prefs = readStoredPreferences();
            await connection.invoke("SkipConversation", conversationId, prefs.mode, prefs.interests, prefs.language);
        } catch (error) {
            console.error("Skip error:", error);
            resetFindingUI();
        } finally {
            skipButton.disabled = false;
        }
    });

    cancelSkipButton.addEventListener("click", async () => {
        try {
            await connection.invoke("CancelMatching");
        } catch (error) {
            console.error("Cancel skip error:", error);
        }

        resetFindingUI();
        endedTitle.textContent = "Conversation ended";
        endedMessage.textContent = "You stopped looking for someone new.";
    });

    function readStoredPreferences() {
        try {
            return {
                mode: sessionStorage.getItem("ct_mode") || "any",
                interests: sessionStorage.getItem("ct_interests") || "",
                language: sessionStorage.getItem("ct_language") || ""
            };
        } catch (e) {
            return { mode: "any", interests: "", language: "" };
        }
    }

    // ---------- LOGOUT MID-CONVERSATION ----------

    const logoutForm = document.getElementById("logoutForm");

    if (logoutForm) {
        logoutForm.addEventListener("submit", async (event) => {
            event.preventDefault();

            try {
                if (connection.state === signalR.HubConnectionState.Connected) {
                    await connection.invoke("LogoutFromChat", conversationId);
                }
            } catch (error) {
                console.error("Logout SignalR error:", error);
            }

            logoutForm.submit();
        });
    }

    // ---------- REPORTING ----------

    let selectedReason = null;

    function openReportModal() {
        selectedReason = null;
        reportDetails.value = "";
        reportReasonButtons.forEach((btn) => btn.classList.remove("selected"));
        confirmReportButton.disabled = true;
        reportModal.classList.add("show");
    }

    function closeReportModal() {
        reportModal.classList.remove("show");
    }

    if (reportButton) reportButton.addEventListener("click", openReportModal);
    if (reportFromEndedButton) reportFromEndedButton.addEventListener("click", openReportModal);
    cancelReportButton.addEventListener("click", closeReportModal);

    reportReasonButtons.forEach((btn) => {
        btn.addEventListener("click", () => {
            reportReasonButtons.forEach((b) => b.classList.remove("selected"));
            btn.classList.add("selected");
            selectedReason = parseInt(btn.dataset.reason, 10);
            confirmReportButton.disabled = false;
        });
    });

    confirmReportButton.addEventListener("click", async () => {
        if (selectedReason === null) return;

        confirmReportButton.disabled = true;
        confirmReportButton.textContent = "Sending...";

        try {
            await connection.invoke("SubmitReport", conversationId, selectedReason, reportDetails.value.trim());
        } catch (error) {
            console.error("Report error:", error);
        }
    });

    connection.on("ReportSubmitted", () => {
        confirmReportButton.textContent = "Report sent";
        setTimeout(() => {
            closeReportModal();
            confirmReportButton.textContent = "Submit report";
        }, 900);
    });

    // ==========================================
    // WEBRTC VOICE
    // ==========================================

    let peerConnection = null;
    let localStream = null;
    let remoteAudio = null;
    let remoteDescriptionSet = false;
    let pendingIceCandidates = [];
    let cachedIceServers = null;
    let sawRelayCandidate = false;

    let callTimerInterval = null;
    let callSeconds = 0;
    let callStartedAt = null;

    // If ICE hasn't succeeded by now the call is not going to connect on its
    // own -- without this the UI sits on "Connecting..." forever, because a
    // stalled ICE agent stays in "checking" rather than reporting "failed".
    const CALL_CONNECT_TIMEOUT_MS = 25000;
    let callConnectTimeout = null;

    function startCallTimeout() {
        clearCallTimeout();

        callConnectTimeout = setTimeout(() => {
            if (!peerConnection || peerConnection.connectionState === "connected") return;

            failCall(
                sawRelayCandidate
                    ? "Couldn't connect. Try again in a moment."
                    : "Couldn't connect — your networks are blocking the call."
            );
        }, CALL_CONNECT_TIMEOUT_MS);
    }

    function clearCallTimeout() {
        if (callConnectTimeout !== null) {
            clearTimeout(callConnectTimeout);
            callConnectTimeout = null;
        }
    }

    function failCall(reason) {
        clearCallTimeout();
        callStatus.textContent = reason;
        stopCallTimer();
        stopQualitySampling();
    }

    async function getIceServers() {
        if (cachedIceServers) return cachedIceServers;

        try {
            const response = await fetch("/Chat/IceServers");
            const data = await response.json();
            cachedIceServers = data.iceServers;
        } catch (error) {
            console.error("Could not load ICE servers, falling back to STUN only.", error);
            cachedIceServers = [{ urls: "stun:stun.l.google.com:19302" }];
        }

        return cachedIceServers;
    }

    async function setupVoiceConnection() {
        try {
            localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });

            const iceServers = await getIceServers();
            peerConnection = new RTCPeerConnection({ iceServers });

            peerConnection.onicecandidate = (event) => {
                if (!event.candidate) return;

                if (event.candidate.type === "relay") sawRelayCandidate = true;

                // Not awaited: this fires from the ICE agent, and an
                // unhandled rejection here would silently drop a candidate
                // and quietly cost us a working connection.
                connection
                    .invoke("SendIceCandidate", conversationId, JSON.stringify(event.candidate))
                    .catch((error) => console.error("Could not deliver ICE candidate:", error));
            };

            // Surfaces the real reason a STUN/TURN server didn't work
            // (401 = bad TURN credentials, 701 = unreachable) instead of
            // leaving the call to hang with no explanation.
            peerConnection.onicecandidateerror = (event) => {
                console.warn(
                    `[WebRTC] ICE server ${event.url} failed: ${event.errorCode} ${event.errorText}`
                );
            };

            peerConnection.oniceconnectionstatechange = () => {
                if (peerConnection.iceConnectionState === "failed") {
                    failCall("Couldn't establish a connection.");
                }
            };

            localStream.getTracks().forEach((track) => peerConnection.addTrack(track, localStream));

            peerConnection.ontrack = (event) => {
                if (!remoteAudio) {
                    remoteAudio = document.createElement("audio");
                    remoteAudio.autoplay = true;
                    remoteAudio.playsInline = true;
                    document.body.appendChild(remoteAudio);
                }

                remoteAudio.srcObject = event.streams[0];
            };

            peerConnection.onconnectionstatechange = () => {
                const state = peerConnection.connectionState;

                if (state === "connected") {
                    clearCallTimeout();
                    callStatus.textContent = "Connected";
                    startCallTimer();
                    startQualitySampling();
                } else if (state === "failed") {
                    failCall("Connection failed");
                } else if (state === "disconnected") {
                    callStatus.textContent = "Reconnecting...";
                    stopQualitySampling();
                }
            };

            // Flush any ICE candidates that arrived before this peer
            // connection existed (common when the callee is slightly slower
            // to accept than the caller's negotiation).
            await flushPendingIceCandidates();
        } catch (error) {
            console.error("Microphone/WebRTC error:", error);
            alert("Microphone permission is required for voice calls.");
            throw error;
        }
    }

    async function flushPendingIceCandidates() {
        if (!peerConnection || !remoteDescriptionSet) return;

        while (pendingIceCandidates.length) {
            const raw = pendingIceCandidates.shift();

            try {
                await peerConnection.addIceCandidate(new RTCIceCandidate(JSON.parse(raw)));
            } catch (error) {
                console.error("Failed to add buffered ICE candidate:", error);
            }
        }
    }

    // ---------- CONNECTION QUALITY ----------

    let qualityInterval = null;
    let lastQualitySample = null;

    function startQualitySampling() {
        stopQualitySampling();
        lastQualitySample = null;
        qualityIndicator.style.display = "inline-flex";
        qualityInterval = setInterval(sampleConnectionQuality, 3000);
        sampleConnectionQuality();
    }

    function stopQualitySampling() {
        if (qualityInterval !== null) {
            clearInterval(qualityInterval);
            qualityInterval = null;
        }
        qualityIndicator.style.display = "none";
        qualityIndicator.className = "quality-indicator";
        lastQualitySample = null;
    }

    async function sampleConnectionQuality() {
        if (!peerConnection) return;

        try {
            const stats = await peerConnection.getStats();
            let packetsLost = 0;
            let packetsReceived = 0;
            let roundTripTime = null;

            stats.forEach((report) => {
                if (report.type === "inbound-rtp" && report.kind === "audio") {
                    packetsLost += report.packetsLost || 0;
                    packetsReceived += report.packetsReceived || 0;
                }

                if (
                    report.type === "candidate-pair" &&
                    report.state === "succeeded" &&
                    typeof report.currentRoundTripTime === "number"
                ) {
                    roundTripTime = report.currentRoundTripTime;
                }
            });

            // packetsLost/packetsReceived are cumulative since the call
            // started, so compare against the previous sample to get a
            // ratio for just this window -- otherwise one bad moment early
            // in a long call would keep the indicator stuck on "poor"
            // forever even after the network recovers.
            let lossRatio = 0;

            if (lastQualitySample) {
                const deltaLost = packetsLost - lastQualitySample.packetsLost;
                const deltaReceived = packetsReceived - lastQualitySample.packetsReceived;
                const deltaTotal = deltaLost + deltaReceived;

                if (deltaTotal > 0) lossRatio = deltaLost / deltaTotal;
            }

            lastQualitySample = { packetsLost, packetsReceived };

            let quality = "good";

            if (lossRatio > 0.08 || (roundTripTime !== null && roundTripTime > 0.5)) {
                quality = "poor";
            } else if (lossRatio > 0.02 || (roundTripTime !== null && roundTripTime > 0.25)) {
                quality = "fair";
            }

            qualityIndicator.className = "quality-indicator " + quality;
        } catch (error) {
            console.error("Quality sampling error:", error);
        }
    }

    function startCallTimer() {
        stopCallTimer();
        callSeconds = 0;
        callStartedAt = Date.now();
        callTimer.textContent = "00:00";
        callTimer.style.display = "block";
        callBarTimer.textContent = "00:00";

        callTimerInterval = setInterval(() => {
            callSeconds++;
            const minutes = Math.floor(callSeconds / 60);
            const seconds = callSeconds % 60;
            const text = String(minutes).padStart(2, "0") + ":" + String(seconds).padStart(2, "0");
            callTimer.textContent = text;
            callBarTimer.textContent = text;
        }, 1000);
    }

    function stopCallTimer() {
        if (callTimerInterval !== null) {
            clearInterval(callTimerInterval);
            callTimerInterval = null;
        }
    }

    function currentCallDurationSeconds() {
        if (!callStartedAt) return 0;
        return Math.round((Date.now() - callStartedAt) / 1000);
    }

    function cleanupVoiceCall() {
        stopCallTimer();
        stopQualitySampling();
        clearCallTimeout();
        callTimer.style.display = "none";
        callStartedAt = null;
        callBar.classList.remove("show");

        if (localStream) {
            localStream.getTracks().forEach((track) => track.stop());
            localStream = null;
        }

        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }

        if (remoteAudio) {
            remoteAudio.srcObject = null;
            remoteAudio.remove();
            remoteAudio = null;
        }

        remoteDescriptionSet = false;
        pendingIceCandidates = [];
        sawRelayCandidate = false;
        isMuted = false;
        setMuteIcon();
    }

    function resetCallScreenForNextCall() {
        callContent.style.display = "flex";
        callEndedState.style.display = "none";
        muteCallButton.style.display = "";
        endCallButton.style.display = "";
        callScreen.style.display = "none";
        callBar.classList.remove("show");
        callLaunchArea.style.display = "";
        startCallButton.disabled = false;
    }

    startCallButton.addEventListener("click", async () => {
        try {
            startCallButton.disabled = true;
            callLaunchArea.style.display = "none";
            callScreen.style.display = "flex";
            callStatus.textContent = "Calling...";
            callBarName.textContent = strangerName.textContent;

            await setupVoiceConnection();
            await connection.invoke("StartVoiceCall", conversationId);
        } catch (error) {
            console.error("Start voice call error:", error);
            callScreen.style.display = "none";
            callLaunchArea.style.display = "";
            startCallButton.disabled = false;
        }
    });

    async function endCall() {
        try {
            await connection.invoke("EndVoiceCall", conversationId, currentCallDurationSeconds());
            cleanupVoiceCall();
            showCallEndedScreen("You ended the voice call.");
        } catch (error) {
            console.error("End voice call error:", error);
        }
    }

    endCallButton.addEventListener("click", endCall);
    callBarEndButton.addEventListener("click", endCall);

    let isMuted = false;

    function setMuteIcon() {
        const icon = isMuted ? "🔇" : "🎙️";
        muteCallButton.textContent = icon;
        callBarMuteButton.textContent = icon;
    }

    function toggleMute() {
        if (!localStream) return;

        isMuted = !isMuted;
        localStream.getAudioTracks().forEach((track) => (track.enabled = !isMuted));
        setMuteIcon();
    }

    muteCallButton.addEventListener("click", toggleMute);
    callBarMuteButton.addEventListener("click", toggleMute);

    // ---------- MINIMIZE / EXPAND ----------
    // Lets the call keep running (audio is unaffected either way, since it's
    // peer-to-peer) while the user drops back into the full chat view.

    minimizeCallButton.addEventListener("click", () => {
        callBarName.textContent = callName.textContent;
        callScreen.style.display = "none";
        callBar.classList.add("show");
    });

    callBarExpandButton.addEventListener("click", () => {
        callBar.classList.remove("show");
        callScreen.style.display = "flex";
    });

    connection.on("IncomingVoiceCall", () => {
        incomingCallScreen.style.display = "flex";
    });

    acceptCallButton.addEventListener("click", async () => {
        incomingCallScreen.style.display = "none";
        resetCallScreenForNextCall();
        callLaunchArea.style.display = "none";
        callScreen.style.display = "flex";
        callStatus.textContent = "Connecting...";
        callBarName.textContent = strangerName.textContent;

        try {
            await setupVoiceConnection();
            const offer = await peerConnection.createOffer();
            await peerConnection.setLocalDescription(offer);
            startCallTimeout();
            await connection.invoke("SendVoiceOffer", conversationId, JSON.stringify(offer));
        } catch (error) {
            console.error("Accept call error:", error);
            callScreen.style.display = "none";
            callLaunchArea.style.display = "";
        }
    });

    declineCallButton.addEventListener("click", async () => {
        incomingCallScreen.style.display = "none";
        try {
            await connection.invoke("DeclineVoiceCall", conversationId);
        } catch (error) {
            console.error("Decline call error:", error);
        }
    });

    connection.on("VoiceCallDeclined", () => {
        callStatus.textContent = "Call declined";
        cleanupVoiceCall();
        setTimeout(() => {
            callScreen.style.display = "none";
            callLaunchArea.style.display = "";
            startCallButton.disabled = false;
        }, 1200);
    });

    function showCallEndedScreen(message) {
        callBar.classList.remove("show");
        callScreen.style.display = "flex";
        callContent.style.display = "none";
        callEndedState.style.display = "flex";
        callEndedMessage.textContent = message;
        startCallButton.disabled = false;
    }

    closeCallButton.addEventListener("click", () => {
        callScreen.style.display = "none";
        resetCallScreenForNextCall();
    });

    connection.on("ReceiveVoiceOffer", async (offer) => {
        // An offer can arrive after we've hung up or declined, in which case
        // there is no peer connection left to apply it to.
        if (!peerConnection) return;

        try {
            const remoteOffer = JSON.parse(offer);
            await peerConnection.setRemoteDescription(new RTCSessionDescription(remoteOffer));
            remoteDescriptionSet = true;
            await flushPendingIceCandidates();

            const answer = await peerConnection.createAnswer();
            await peerConnection.setLocalDescription(answer);

            callStatus.textContent = "Connecting...";
            startCallTimeout();

            await connection.invoke("SendVoiceAnswer", conversationId, JSON.stringify(answer));
        } catch (error) {
            console.error("Handle offer error:", error);
            failCall("Couldn't start the call.");
        }
    });

    connection.on("ReceiveVoiceAnswer", async (answer) => {
        if (!peerConnection) return;

        try {
            const remoteAnswer = JSON.parse(answer);
            await peerConnection.setRemoteDescription(new RTCSessionDescription(remoteAnswer));
            remoteDescriptionSet = true;
            await flushPendingIceCandidates();

            callStatus.textContent = "Connecting...";
        } catch (error) {
            console.error("Handle answer error:", error);
            failCall("Couldn't start the call.");
        }
    });

    connection.on("ReceiveIceCandidate", async (candidate) => {
        if (peerConnection && remoteDescriptionSet) {
            try {
                await peerConnection.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)));
            } catch (error) {
                console.error("Add ICE candidate error:", error);
            }
        } else {
            // The peer connection does not exist yet (or the remote
            // description has not landed), so hold onto this candidate
            // instead of silently discarding it.
            pendingIceCandidates.push(candidate);
        }
    });

    connection.on("VoiceCallEnded", () => {
        cleanupVoiceCall();
        showCallEndedScreen("The other user ended the voice call.");
    });

    // ---------- LEAVE ON TAB CLOSE ----------
    // A SignalR invoke can't be trusted to finish before the page dies mid
    // unload, so this is a plain HTTP fallback via sendBeacon (fire-and-
    // forget, survives navigation) that ends the conversation server-side.

    window.addEventListener("pagehide", (event) => {
        // event.persisted means the page is going into the back/forward
        // cache rather than being torn down -- which is also what happens
        // when a mobile user switches apps mid-call. Ending the
        // conversation there would hang up on them for backgrounding the
        // browser, so only a real teardown counts as leaving.
        if (event.persisted) return;

        if (conversationEnded || !requestToken) return;

        const formData = new FormData();
        formData.append("id", conversationId);
        formData.append("__RequestVerificationToken", requestToken);

        navigator.sendBeacon("/Chat/LeaveOnUnload", formData);
    });

    // ---------- START ----------

    startConnection();
})();
