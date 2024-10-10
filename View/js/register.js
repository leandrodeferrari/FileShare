(() => {
    document.getElementById("register-form").addEventListener("submit", function (event) {
        event.preventDefault();

        window.location.href = "login.html";
    });
})();