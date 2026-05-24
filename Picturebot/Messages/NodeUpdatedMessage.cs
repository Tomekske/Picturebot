using CommunityToolkit.Mvvm.Messaging.Messages;
using Database.Domain.Entities;

namespace Picturebot.Messages;

public class NodeUpdatedMessage(Node node) : ValueChangedMessage<Node>(node);
