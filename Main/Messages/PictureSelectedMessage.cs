using CommunityToolkit.Mvvm.Messaging.Messages;
using Main.ViewModels;

namespace Main.Messages;

public class PictureSelectedMessage(PictureItemViewModel picture) : ValueChangedMessage<PictureItemViewModel>(picture);
