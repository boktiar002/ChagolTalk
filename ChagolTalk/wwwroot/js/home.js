// ==========================================
// HOME PAGE -- quick-start modal
// ==========================================

(function () {
    "use strict";

    const openButton = document.getElementById("openQuickStartButton");
    const modal = document.getElementById("quickStartModal");

    if (!openButton || !modal) return;

    const cancelButton = document.getElementById("cancelQuickStartButton");
    const nameInput = document.getElementById("quickStartName");
    const form = document.getElementById("quickStartForm");
    const submitButton = document.getElementById("confirmQuickStartButton");

    function openModal() {
        modal.classList.add("show");
        setTimeout(() => nameInput.focus(), 50);
    }

    function closeModal() {
        modal.classList.remove("show");
    }

    // Exposed so the server can reopen the modal (with its error message
    // already in the markup) after a redirect-back from a failed submit,
    // without needing a client-side fetch/JSON round trip.
    window.__ctOpenQuickStart = openModal;

    openButton.addEventListener("click", openModal);
    cancelButton.addEventListener("click", closeModal);

    modal.addEventListener("click", (event) => {
        if (event.target === modal) closeModal();
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && modal.classList.contains("show")) closeModal();
    });

    form.addEventListener("submit", () => {
        submitButton.disabled = true;
        submitButton.textContent = "Starting...";
    });
})();
