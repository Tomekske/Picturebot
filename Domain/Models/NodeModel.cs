using Domain.Enums;

namespace Domain.Models;

public class NodeModel {
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public NodeModel? Parent { get; set; }

    public NodeType Type { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<NodeModel>? Children { get; set; } = new List<NodeModel>();
}
