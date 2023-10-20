using System.Text.Json.Serialization;

namespace Chat.Api;

// Required for polymorphic deserialization
[JsonDerivedType(typeof(ChatMessage))]
[JsonDerivedType(typeof(UserList))]
[JsonDerivedType(typeof(UserConnected))]
[JsonDerivedType(typeof(UserDisconnected))]
[JsonDerivedType(typeof(History))]
record Message(string Type);

record ChatMessage(string Name, string Content) : Message(nameof(ChatMessage));

record UserList(string[] Users) : Message(nameof(UserList));

record UserConnected(string Name) : Message(nameof(UserConnected));

record UserDisconnected(string Name) : Message(nameof(UserDisconnected));

record History(Message[] Messages) : Message(nameof(History));