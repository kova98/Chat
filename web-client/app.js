let socket = null;

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
    setStatus('Connecting...');
    
    socket.onopen = goToChat;

    socket.onerror = function (event) {
        setStatus('Could not reach server.');
    }

    socket.onmessage = function (event) {
        const messageObject = JSON.parse(event.data);
        const message = document.createElement('div');
        message.innerText = messageObject.Name + ': ' + messageObject.Content;
        
        const messages = document.getElementById('messages');
        messages.appendChild(message);
    };

    socket.onclose = function (event) {
        goToIndex();
        console.log('WebSocket is closed.', event);
        let status = event.reason === '' ? 'Connection closed.' : event.reason;
        setStatus(status);
    };
}

function sendMessage() {
    const nameInput = document.getElementById('nameInput');
    const messageInput = document.getElementById('messageInput');
    const nameText = nameInput.value.trim();
    const messageText = messageInput.value.trim();
    if (nameText && messageText) {
        const messageObj = { Name: nameText, Content: messageText };
        socket.send(JSON.stringify(messageObj));
        messageInput.value = '';
        messageInput.focus();
    }
}

function setStatus(statusText) {
    const status = currentPage().querySelector('.status');
    status.innerText = statusText;
    status.hidden = false;
}

function goToIndex() {
    document.getElementById('homePage').style.display = 'block';
    document.getElementById('chatPage').style.display = 'none';
}

function goToChat() {
    document.getElementById('homePage').style.display = 'none';
    document.getElementById('chatPage').style.display = 'block'
}

function currentPage() {
    return document.querySelector('.page[style*="display: block"]');
}




