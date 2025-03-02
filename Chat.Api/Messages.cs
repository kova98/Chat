using System.Text.Json.Serialization;

namespace Chat.Api;

// Required for polymorphic deserialization
[JsonDerivedType(typeof(ChatMessage), typeDiscriminator: "ChatMessage")]
[JsonDerivedType(typeof(UserList), typeDiscriminator: "UserList")]
[JsonDerivedType(typeof(UserConnected), typeDiscriminator: "UserConnected")]
[JsonDerivedType(typeof(UserDisconnected), typeDiscriminator: "UserDisconnected")]
[JsonDerivedType(typeof(History), typeDiscriminator: "History")]
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
public record Message(string Type);

public record ChatMessage(string Name, string Content) : Message(nameof(ChatMessage));

public record UserList(IEnumerable<string> Users) : Message(nameof(UserList));

public record UserConnected(string Name, string Transport) : Message(nameof(UserConnected));

public record UserDisconnected(string Name) : Message(nameof(UserDisconnected));

public record History(IEnumerable<Message> Messages) : Message(nameof(History));