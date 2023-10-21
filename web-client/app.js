let socket = null;
let users = [];
let name = localStorage.getItem('name');
let history = [];
let transport = localStorage.getItem('transport');
if (!transport) {
    transport = 'web-socket';
}

function selectTransport(t) {
    transport = t;
    localStorage.setItem('transport', transport);
    document.querySelectorAll('.transport-button').forEach(button => {
        button.classList.toggle('selected', button.dataset.transport === transport);
    });
}

selectTransport(transport);

 let wsServerAddress = 'wss://playground.rokokovac.com/chat';
 let httpServerAddress = 'https://playground.rokokovac.com/chat';
//let wsServerAddress = 'ws://localhost:5000';
//let httpServerAddress = 'http://localhost:5000';

const nameInput = document.getElementById('nameInput');
nameInput.focus();
nameInput.value = name;

nameInput.addEventListener('keypress', function (event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        if (name.length > 16) {
            return;
        }
        event.preventDefault();
        connect();
    }
});

const connectButton = document.getElementById('connectButton');
nameInput.addEventListener("input", function (event) {
    setStatus('');
    connectButton.disabled = false;
    name = nameInput.value.trim();
    if (name.length > 16) {
        setStatus('Name is too long');
        connectButton.disabled = true;
        return;
    }
});

const messageInput = document.getElementById('messageInput');
messageInput.addEventListener('keypress', function (event) {
    const messageText = messageInput.value.trim();
    if (event.key === 'Enter' && !event.shiftKey) {
        if (messageText.length > 256) {
            return;
        }

        event.preventDefault();
        sendMessage();
    }
});

messageInput.addEventListener("input", function (event) {
    const error = document.getElementById('error-message');
    error.hidden = true;
    sendButton.disabled = false;
    const messageText = messageInput.value.trim();
    if (messageText.length > 256) {
        error.innerText = 'Message is too long';
        error.hidden = false;
        const sendButton = document.getElementById('sendButton');
        sendButton.disabled = true;
        return;
    }
});

let polling = false;

function connect() {
    name = nameInput.value.trim();
    if (!name) {
        setStatus('Please enter a name.');
        return;
    }

    if (name.length > 16) {
        setStatus('Name is too long.');
        return;
    }

    localStorage.setItem("name", name);
    
    setStatus('Connecting...');

    switch (transport) {
        case 'long-polling':
            if (!polling){
                polling = true;
                connectLongPolling();
            }
            break;
        case 'web-socket':
            connectWebSocket();
            break;
        default:
            console.error('Unknown transport: ' + transport);
    }
}

let connectionId = null;

async function connectLongPolling() {
    const nameParam = encodeURIComponent(name);
    const url = `${httpServerAddress}/lp?name=${nameParam}&id=${connectionId}`;

    try {
        const response = await fetch(url);

        if (response.status == 204) {
            // Timed out. No messages.
        } else if (!response.ok) {  // Check if response status is not 2xx
            const errorText = await response.text();
            polling = false;
            goToIndex();
            setStatus(errorText);
            return;
        } else {
            const responseData = await response.json();
            for (let i = 0; i < responseData.length; i++) {
                handleMessage(responseData[i]);
            }
            polling = false;
            goToChat();
            // if (!connected) {
            //     goToChat();
            //     connected = true;
            // }
        }
        
        connectionId = response.headers.get('X-Connection-Id');
        connectLongPolling();
    } catch (error) {
        console.error('Error:', error);
        // Handle network error or other fetch exceptions
        polling = false;
        goToIndex();
        setStatus('Network error');
    }
}

function connectWebSocket() {
    const nameParam = encodeURIComponent(name);
    socket = new WebSocket(wsServerAddress + '/ws?name=' + nameParam);

    socket.onopen = function (event) {
        goToChat();
        messageInput.focus();
    }

    socket.onerror = function (event) {
        setStatus('Could not reach server.');
    }

    socket.onmessage = function (event) {
        const message = JSON.parse(event.data);
        handleMessage(message);
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

function handleMessage(message) {
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
        const message = {Type: 'ChatMessage', Name: name, Content: messageText}
        switch (transport) {
            case 'long-polling':
                sendMessageLongPolling(message);
                break;
            case 'web-socket':
                sendMessageWebSocket(JSON.stringify(message));
                break;
            default:
                console.log('Unknown transport: ' + transport);
        }
        messageInput.value = '';
        messageInput.focus();
    }
}

function sendMessageLongPolling(message) {
    fetch(httpServerAddress + '/lp/message', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(message)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error(error);
            }
        })
        .catch(error => {
            goToIndex();
            setStatus(error);
            console.error('Error:', error);
        });
}

function sendMessageWebSocket(message) {
    socket.send(message);
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