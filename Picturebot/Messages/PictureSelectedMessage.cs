using CommunityToolkit.Mvvm.Messaging.Messages;
using Picturebot.ViewModels;

namespace Picturebot.Messages;

public class PictureSelectedMessage(PictureItemViewModel? picture) : ValueChangedMessage<PictureItemViewModel?>(picture);
