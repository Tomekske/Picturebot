using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Picturebot.ViewModels;

namespace Picturebot.Messages;

public class PictureSelectionChangedMessage(List<PictureItemViewModel> selectedPictures) 
    : ValueChangedMessage<List<PictureItemViewModel>>(selectedPictures);
