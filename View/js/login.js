(() => {
    document.getElementById("login-form").addEventListener("submit", function (event) {
        event.preventDefault();

        window.location.href = "dashboard.html";
    });
})();