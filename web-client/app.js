let socket = null;
let users = [];
let name = null;

const messageInput = document.getElementById('messageInput');
messageInput.focus();
messageInput.addEventListener('keypress', function (event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        sendMessage();
    }
});

function connect () {
    // const nameInput = document.getElementById('nameInput');
    name = nameInput.value.trim();
    const nameParam = encodeURIComponent(name);
    socket = new WebSocket('ws://localhost:5000/ws?name=' + nameParam);
    setStatus('Connecting...');
    
    socket.onopen = goToChat;

    socket.onerror = function (event) {
        setStatus('Could not reach server.');
    }
 
    socket.onmessage = function (event) {
        const message = JSON.parse(event.data);
        switch (message.Type) {
            case 'ChatMessage':
                handleChatMessage(message);
                break;
            case 'UserList':
                handleUserList(message);
                break;
            default: 
                console.log('Unknown message type: ' + message.Type);
        }
    };

    socket.onclose = function (event) {
        goToIndex();
        console.log('WebSocket is closed.', event);
        let status = event.reason === '' ? 'Connection closed.' : event.reason;
        setStatus(status);
    };
}

function handleUserList(message){
    users = message.Users;
    updateUsersDisplay();
}

function updateUsersDisplay() {
    const usersList = document.getElementById('users');
    usersList.innerHTML = '';
    for (let i = 0; i < users.length; i++) {
        const userElement = document.createElement('div');
        userElement.className = 'user';
        userElement.innerText = users[i];
        if (users[i] === name) {
            userElement.classList.add('current-user');
        }
        usersList.appendChild(userElement);
    }
}

function handleChatMessage(messageObject) {
    const message = document.createElement('div');
    message.innerText = messageObject.Name + ': ' + messageObject.Content;

    const messages = document.getElementById('messages');
    messages.appendChild(message);
}

function sendMessage() {
    const messageInput = document.getElementById('messageInput');
    const messageText = messageInput.value.trim();
    if (name && messageText) {
        const messageObj = { Type: 'ChatMessage', Name: name, Content: messageText}
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




