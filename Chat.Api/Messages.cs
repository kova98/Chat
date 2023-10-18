namespace Chat.Api;

record Message(string Type);

record ChatMessage(string Name, string Content) : Message("ChatMessage");

record UserList(string[] Users) : Message("UserList");