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
    const yearInput = document.getElementById("quickStartYear");
    const form = document.getElementById("quickStartForm");
    const submitButton = document.getElementById("confirmQuickStartButton");

    function openModal() {
        modal.classList.add("show");

        // Reopening after a rejected submit comes back with the fields already
        // filled, so land on whichever one still needs attention rather than
        // always dropping the cursor at the top.
        setTimeout(() => {
            const target = !nameInput.value && yearInput && yearInput.value
                ? nameInput
                : yearInput && !yearInput.value
                    ? yearInput
                    : nameInput;

            target.focus();
        }, 50);
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

    // The browser's own message for a max violation reads "Value must be less
    // than or equal to 2013", which explains nothing. Say what the rule is
    // instead, and say it as they type rather than after a round trip.
    if (yearInput) {
        const latestYear = Number(yearInput.max);
        const minimumAge = Number(yearInput.dataset.minAge);

        const checkYear = () => {
            const entered = Number(yearInput.value);

            yearInput.setCustomValidity(
                yearInput.value && entered > latestYear
                    ? `You need to be at least ${minimumAge} to use ChagolTalk.`
                    : ""
            );
        };

        yearInput.addEventListener("input", checkYear);
        checkYear();
    }

    form.addEventListener("submit", () => {
        submitButton.disabled = true;
        submitButton.textContent = "Starting...";
    });
})();
