using Database.Domain.Entities;

namespace Picturebot.Services;

/// <summary>
///     Navigation through the library's hierarchy, driven by the mouse side buttons, Alt+Left/Right, Backspace and the breadcrumbs.
///     <para>
///         CONTRACT (NavigationServiceTests pins it; keep it working when changing navigation, selection or node loading):
///         <list type="bullet">
///             <item>Back (mouse button 4) goes one level UP: Montreal › Olympic Park › Biodome → Olympic Park → Montreal → Library.</item>
///             <item>Back works for any node, also one that only knows its ParentId (new or imported) and not its Parent.</item>
///             <item>Forward (mouse button 5) goes back DOWN into the child that Back came from, one level per press.</item>
///             <item>Picking anything else (tree, breadcrumb, new album) starts a new path and clears Forward.</item>
///             <item>Deleting a node removes it, and what's inside it, from the path; deleting the current node goes to its parent.</item>
///         </list>
///     </para>
/// </summary>
public interface INavigationService {
    Node? CurrentNode { get; }

    /// <param name="addToHistory">False keeps the Forward path, for callers that restore a position instead of navigating.</param>
    void NavigateTo(Node? node, bool addToHistory = true);

    void GoBack();
    void GoForward();
    void GoUp();
    bool CanGoBack { get; }
    bool CanGoForward { get; }
}
