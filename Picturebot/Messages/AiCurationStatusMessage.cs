using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Picturebot.Messages;

public record AiCurationStatus(bool IsCurating, string StatusText = "AI Vision Curation in progress...");

public class AiCurationStatusMessage(AiCurationStatus value) : ValueChangedMessage<AiCurationStatus>(value);
