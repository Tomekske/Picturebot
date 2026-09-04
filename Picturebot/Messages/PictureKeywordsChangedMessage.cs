using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Picturebot.ViewModels;

namespace Picturebot.Messages;

public class PictureKeywordsChangedMessage(List<PictureItemViewModel> pictures) 
    : ValueChangedMessage<List<PictureItemViewModel>>(pictures);
