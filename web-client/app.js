let socket = null;
let users = [];
let name = localStorage.getItem('name');

let serverAddress = 'wss://playground.rokokovac.com/chat';
// let serverAddress = 'ws://localhost:5000';

const nameInput = document.getElementById('nameInput');
nameInput.focus();
nameInput.value = name;

nameInput.addEventListener('keypress', function (event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        if (name.length > 16){
            return;
        }
        event.preventDefault();
        connect();
    }
});

const connectButton = document.getElementById('connectButton');
nameInput.addEventListener("input", function(event) {
    setStatus('');
    connectButton.disabled = false;
    name = nameInput.value.trim();
    if (name.length > 16){
        setStatus('Name is too long');
        connectButton.disabled = true;
        return;
    }
});

const messageInput = document.getElementById('messageInput');
messageInput.addEventListener('keypress', function (event) {
    const messageText = messageInput.value.trim();
    if (event.key === 'Enter' && !event.shiftKey) {
        if (messageText.length > 256){
            return;
        }
        
        event.preventDefault();
        sendMessage();
    }
});

messageInput.addEventListener("input", function(event) {
    const error = document.getElementById('error-message');
    error.hidden = true;
    sendButton.disabled = false;
    const messageText = messageInput.value.trim();
    if (messageText.length > 256){
        error.innerText = 'Message is too long';
        error.hidden = false;
        const sendButton = document.getElementById('sendButton');
        sendButton.disabled = true;
        return;
    }
});

function connect() {
    name = nameInput.value.trim();
    if (!name) {
        setStatus('Please enter the name.');
        return;
    }
    
    if (name.length > 16) {
        setStatus('Name is too long.');
        return;
    }
    
    localStorage.setItem("name", name);
    const nameParam = encodeURIComponent(name);
    socket = new WebSocket(serverAddress + '/ws?name=' + nameParam);
    setStatus('Connecting...');

    socket.onopen = function (event) {
        goToChat();
        messageInput.focus();
    }

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
            case 'History':
                handleHistory(message);
                break;
            case 'UserConnected':
                handleUserConnected(message);
                break;
            case 'UserDisconnected':
                handleUserDisconnected(message);
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
        clearHistory();
        users = [];
        updateUsersDisplay();
    };
}

function clearHistory() {
    const messages = document.getElementById('messages');
    messages.innerHTML = '';
}

function handleHistory(message) {
    for (let i = 0; i < message.Messages.length; i++) {
        switch (message.Messages[i].Type) {
            case 'ChatMessage':
                handleChatMessage(message.Messages[i]);
                break;
            case 'UserConnected':
                handleUserConnected(message.Messages[i]);
                break;
            case 'UserDisconnected':
                handleUserDisconnected(message.Messages[i]);
                break;
            default:
                console.log('Unknown message type: ' + message.Messages[i].Type);
        }
    }
}

function handleUserConnected(message) {
    users.push(message.Name);
    updateUsersDisplay();
    addMessage('* ' + message.Name + ' connected.', 'status-message');
}

function handleUserDisconnected(message) {
    const index = users.indexOf(message.Name);
    if (index > -1) {
        users.splice(index, 1);
    }
    updateUsersDisplay();
    addMessage('* ' + message.Name + ' disconnected.', 'status-message');
}

function handleUserList(message) {
    users = message.Users;
    updateUsersDisplay();
}

function updateUsersDisplay() {
    const usersList = document.getElementsByClassName('users');
    for (let i = 0; i < usersList.length; i++) {
        usersList[i].innerHTML = '';
        for (let j = 0; j < users.length; j++) {
            const userElement = document.createElement('div');
            userElement.className = 'user';
            userElement.innerText = users[j];
            if (users[j] === name) {
                userElement.classList.add('current-user');
            }
            usersList[i].appendChild(userElement);
        }
    }
}

function handleChatMessage(messageObject) {
    const message = document.createElement('div');

    const messageName = document.createElement('span');
    messageName.innerText = messageObject.Name + ': ';
    messageName.className = 'message-name';
    message.appendChild(messageName);

    const messageContent = document.createElement('span');
    messageContent.innerText = messageObject.Content;
    messageContent.className = 'message-content';
    message.appendChild(messageContent);

    const messages = document.getElementById('messages');
    messages.appendChild(message);
    messages.scrollTop = messages.scrollHeight
}
function addMessage(text, type) {
    const message = document.createElement('div');
    message.innerText = text;
    message.className = type || 'message';
    
    const messages = document.getElementById('messages');
    messages.appendChild(message);
    messages.scrollTop = messages.scrollHeight
}

function sendMessage() {
    const messageInput = document.getElementById('messageInput');
    const messageText = messageInput.value.trim();
    if (name && messageText) {
        const messageObj = {Type: 'ChatMessage', Name: name, Content: messageText}
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