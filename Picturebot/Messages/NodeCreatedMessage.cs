using CommunityToolkit.Mvvm.Messaging.Messages;
using Database.Domain.Entities;

namespace Picturebot.Messages;

public class NodeCreatedMessage : ValueChangedMessage<Node> {
    public NodeCreatedMessage(Node value) : base(value) {
    }
}
