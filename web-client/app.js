var socket = null;

const messageInput = document.getElementById('messageInput');
messageInput.focus();
messageInput.addEventListener('keypress', function (event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        sendMessage();
    }
});

function connect () {
    const name = encodeURIComponent(nameInput.value.trim());
    socket = new WebSocket('ws://localhost:5000/ws?name=' + name);

    socket.onopen = goToChat();

    socket.onerror = function (event) {
        const status = document.getElementsByClassName('status');
        status.foreach(function (element) {
            element.innerText = 'Lost connection to the server.';
            element.hidden = false;
        });
            
        status.innerText = 'Lost connection to the server.';
        status.hidden = false;
    }

    socket.onmessage = function (event) {
        const messageObject = JSON.parse(event.data);
        const message = document.createElement('div');
        message.innerText = messageObject.Name + ': ' + messageObject.Content;
        
        const messages = document.getElementById('messages');
        messages.appendChild(message);
    };

    socket.onclose = function (event) {
        console.log('WebSocket is closed.', event);
        const status = document.getElementById('status');
        status.innerText = 'Lost connection to the server.';
        status.hidden = false;
    };
}

function goToIndex() {
    document.getElementById('homePage').style.display = 'block';
    document.getElementById('chatPage').style.display = 'none';
}

function goToChat() {
    document.getElementById('homePage').style.display = 'none';
    document.getElementById('chatPage').style.display = 'block'
}

function sendMessage() {
    const nameInput = document.getElementById('nameInput');
    const messageInput = document.getElementById('messageInput');
    const nameText = nameInput.value.trim();
    const messageText = messageInput.value.trim();
    if (nameText && messageText) {
        const messageObj = { Name: nameText, Content: messageText };
        window.socket.send(JSON.stringify(messageObj));
        messageInput.value = '';
        messageInput.focus();
    }
}



