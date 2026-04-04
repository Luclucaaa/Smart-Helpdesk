// Small helper for triggering hidden <input type="file"> from Blazor.
window.SmartHelpdeskClickFileInput = function (id) {
    const el = document.getElementById(id);
    if (el) {
        el.click();
    }
};

