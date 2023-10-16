var socket = new WebSocket('ws://localhost:5000/ws');

socket.onopen = function (event) {
    console.log('WebSocket is connected.', event);
};

socket.onmessage = function (event) {
    var messages = document.getElementById('messages');
    var message = document.createElement('div');
    var messageObject = JSON.parse(event.data);
    message.innerText = messageObject.Name + ': ' + messageObject.Content;
    messages.appendChild(message);
};

socket.onclose = function (event) {
    console.log('WebSocket is closed.', event);
    const status = document.getElementById('status');
    status.innerText = 'Lost connection to the server.';
    status.hidden = false;
};

const nameInput = document.getElementById('nameInput');
const  messageInput = document.getElementById('messageInput');

messageInput.addEventListener('keypress', function (event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        sendMessage(); 
    }
});

function sendMessage() {
    var nameText = nameInput.value.trim();
    var messageText = messageInput.value.trim();
    if (nameText && messageText) {
        var messageObj = { Name: nameText, Content: messageText };
        socket.send(JSON.stringify(messageObj));
        messageInput.value = '';
        messageInput.focus();
    }
}

messageInput.focus();
