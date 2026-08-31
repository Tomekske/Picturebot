using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Picturebot.Messages;

/// <summary>
///     Message dispatched when a global search is initiated or cleared across all albums.
/// </summary>
public class GlobalSearchMessage(string query) : ValueChangedMessage<string>(query);
