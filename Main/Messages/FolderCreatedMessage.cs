using CommunityToolkit.Mvvm.Messaging.Messages;
using Database.Domain.Entities;

namespace Main.Messages;

public class FolderCreatedMessage : ValueChangedMessage<Folder> {
    public FolderCreatedMessage(Folder value) : base(value) {
    }
}
