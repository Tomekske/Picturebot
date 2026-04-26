using Database.Domain.Entities;

namespace Picturebot.Services;

public interface INavigationService {
    Node? CurrentNode { get; }
    void NavigateTo(Node? node, bool addToHistory = true);
    void GoBack();
    void GoForward();
    void GoUp();
    bool CanGoBack { get; }
    bool CanGoForward { get; }
}
