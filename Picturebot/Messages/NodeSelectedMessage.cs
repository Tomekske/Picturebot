using CommunityToolkit.Mvvm.Messaging.Messages;
using Database.Domain.Entities;

namespace Picturebot.Messages;

public class NodeSelectedMessage(Node node) : ValueChangedMessage<Node>(node);
