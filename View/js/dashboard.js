(() => {
    document.getElementById('postButton').addEventListener('click', () => {
        fetch('http://localhost:9999/test', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                name: 'Juan',
                email: 'centurion@gmail.com'
            })
        })
            .then(response => {
                if (response.ok) {
                    console.log('POST realizado correctamente.');
                } else {
                    console.error('Error en la solicitud.');
                }
            })
            .catch(error => console.error('Error:', error));
    });
})();