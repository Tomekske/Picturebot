using CommunityToolkit.Mvvm.Messaging.Messages;
using Database.Domain.Entities;

namespace Main.Messages;

public class NodeCreatedMessage : ValueChangedMessage<Node> {
    public NodeCreatedMessage(Node value) : base(value) {
    }
}
