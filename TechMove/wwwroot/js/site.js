document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".modal").forEach(modal => {
        if (modal.parentElement !== document.body) {
            document.body.appendChild(modal);
        }
    });
});
