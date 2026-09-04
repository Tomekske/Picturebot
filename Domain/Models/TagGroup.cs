using System.Collections.ObjectModel;

namespace Domain.Models;

/// <summary>
///     Represents a UI preset group of tags for quick culling.
/// </summary>
public class TagGroup {
    public Guid GroupId { get; set; } = Guid.NewGuid();
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<Guid> TagIds { get; set; } = new();
    public bool ExcludeFromTraining { get; set; } = false;
}
