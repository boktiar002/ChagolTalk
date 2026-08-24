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
    const reportButton = document.getElementById("reportButton");

    const endedScreen = document.getElementById("endedScreen");
    const endedMessage = document.getElementById("endedMessage");
    const findAnotherButton = document.getElementById("findAnotherButton");
    const reportFromEndedButton = document.getElementById("reportFromEndedButton");

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

    const reportModal = document.getElementById("reportModal");
    const reportReasonButtons = Array.from(document.querySelectorAll(".report-reason"));
    const reportDetails = document.getElementById("reportDetails");
    const cancelReportButton = document.getElementById("cancelReportButton");
    const confirmReportButton = document.getElementById("confirmReportButton");

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

    function showEndedScreen(message) {
        messages.style.display = "none";
        composerWrap.style.display = "none";
        endButton.style.display = "none";
        if (reportButton) reportButton.style.display = "none";

        endedScreen.style.display = "flex";
        endedMessage.textContent = message;
        setStatus("Conversation ended", "danger");

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
        window.location.href = "/Chat/Room?id=" + newConversationId;
    });

    connection.on("WaitingForMatch", () => {
        findAnotherButton.textContent = "Finding someone...";
    });

    connection.on("MatchingBlocked", (reason) => {
        findAnotherButton.disabled = false;
        findAnotherButton.textContent = "Find Someone";
        findAnotherButton.classList.remove("finding");
        alert(reason);
    });

    findAnotherButton.addEventListener("click", async () => {
        try {
            findAnotherButton.disabled = true;
            findAnotherButton.textContent = "Finding someone...";
            findAnotherButton.classList.add("finding");

            const prefs = readStoredPreferences();
            await connection.invoke("FindAnother", prefs.mode, prefs.interests, prefs.language);
        } catch (error) {
            console.error("Find another error:", error);
            findAnotherButton.disabled = false;
            findAnotherButton.textContent = "Find Someone";
            findAnotherButton.classList.remove("finding");
        }
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

    let callTimerInterval = null;
    let callSeconds = 0;
    let callStartedAt = null;

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

            peerConnection.onicecandidate = async (event) => {
                if (!event.candidate) return;

                await connection.invoke(
                    "SendIceCandidate",
                    conversationId,
                    JSON.stringify(event.candidate)
                );
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
                    callStatus.textContent = "Connected";
                    startCallTimer();
                } else if (state === "failed") {
                    callStatus.textContent = "Connection failed";
                    stopCallTimer();
                } else if (state === "disconnected") {
                    callStatus.textContent = "Reconnecting...";
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

    function startCallTimer() {
        stopCallTimer();
        callSeconds = 0;
        callStartedAt = Date.now();
        callTimer.textContent = "00:00";
        callTimer.style.display = "block";

        callTimerInterval = setInterval(() => {
            callSeconds++;
            const minutes = Math.floor(callSeconds / 60);
            const seconds = callSeconds % 60;
            callTimer.textContent = String(minutes).padStart(2, "0") + ":" + String(seconds).padStart(2, "0");
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
        callTimer.style.display = "none";
        callStartedAt = null;

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
    }

    function resetCallScreenForNextCall() {
        callContent.style.display = "flex";
        callEndedState.style.display = "none";
        muteCallButton.style.display = "";
        endCallButton.style.display = "";
        callScreen.style.display = "none";
        startCallButton.disabled = false;
    }

    startCallButton.addEventListener("click", async () => {
        try {
            startCallButton.disabled = true;
            callScreen.style.display = "flex";
            callStatus.textContent = "Calling...";

            await setupVoiceConnection();
            await connection.invoke("StartVoiceCall", conversationId);
        } catch (error) {
            console.error("Start voice call error:", error);
            callScreen.style.display = "none";
            startCallButton.disabled = false;
        }
    });

    endCallButton.addEventListener("click", async () => {
        try {
            await connection.invoke("EndVoiceCall", conversationId, currentCallDurationSeconds());
            cleanupVoiceCall();
            showCallEndedScreen("You ended the voice call.");
        } catch (error) {
            console.error("End voice call error:", error);
        }
    });

    let isMuted = false;

    muteCallButton.addEventListener("click", () => {
        if (!localStream) return;

        isMuted = !isMuted;
        localStream.getAudioTracks().forEach((track) => (track.enabled = !isMuted));
        muteCallButton.textContent = isMuted ? "🔇" : "🎙️";
    });

    connection.on("IncomingVoiceCall", () => {
        incomingCallScreen.style.display = "flex";
    });

    acceptCallButton.addEventListener("click", async () => {
        incomingCallScreen.style.display = "none";
        resetCallScreenForNextCall();
        callScreen.style.display = "flex";
        callStatus.textContent = "Connecting...";

        try {
            await setupVoiceConnection();
            const offer = await peerConnection.createOffer();
            await peerConnection.setLocalDescription(offer);
            await connection.invoke("SendVoiceOffer", conversationId, JSON.stringify(offer));
        } catch (error) {
            console.error("Accept call error:", error);
            callScreen.style.display = "none";
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
            startCallButton.disabled = false;
        }, 1200);
    });

    function showCallEndedScreen(message) {
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
        try {
            const remoteOffer = JSON.parse(offer);
            await peerConnection.setRemoteDescription(new RTCSessionDescription(remoteOffer));
            remoteDescriptionSet = true;
            await flushPendingIceCandidates();

            const answer = await peerConnection.createAnswer();
            await peerConnection.setLocalDescription(answer);
            await connection.invoke("SendVoiceAnswer", conversationId, JSON.stringify(answer));

            callStatus.textContent = "Connecting...";
        } catch (error) {
            console.error("Handle offer error:", error);
        }
    });

    connection.on("ReceiveVoiceAnswer", async (answer) => {
        try {
            const remoteAnswer = JSON.parse(answer);
            await peerConnection.setRemoteDescription(new RTCSessionDescription(remoteAnswer));
            remoteDescriptionSet = true;
            await flushPendingIceCandidates();

            callStatus.textContent = "Connecting...";
        } catch (error) {
            console.error("Handle answer error:", error);
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

    // ---------- START ----------

    startConnection();
})();
