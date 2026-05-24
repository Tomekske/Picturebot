using Database.Domain.Entities;
using Domain.Enums;
using Graph.Domain.Interfaces;

namespace Graph.Infrastructure.Services;

public class FolderService(INodeService nodeService) : IFolderService {
    public async Task<Folder> CreateAsync(int? parentId, string folderName) {
        var folder = new Folder {
            Name = folderName,
            ParentId = parentId,
            Type = NodeType.Folder
        };

        await nodeService.CreateNodeAsync(folder);
        return folder;
    }

    public async Task<List<Folder>> FindAllAsync() {
        var allNodes = await nodeService.GetAllNodesAsync();

        return allNodes.OfType<Folder>()
            .Where(n => n.Type == NodeType.Folder)
            .ToList();
    }

    public async Task DeleteAsync(Folder folder) {
        var hydratedTree = await nodeService.LoadHydratedTreeAsync();
        var nodeInTree = FindNode(hydratedTree, folder.Id);

        if (nodeInTree?.Children != null && nodeInTree.Children.Any()) {
            throw new InvalidOperationException("Cannot delete a folder that is not empty.");
        }

        await nodeService.DeleteNodeAsync(folder);
    }

    private Node? FindNode(List<Node> nodes, int id) {
        foreach (var node in nodes) {
            if (node.Id == id) return node;
            if (node.Children != null) {
                var found = FindNode(node.Children.ToList(), id);
                if (found != null) return found;
            }
        }
        return null;
    }
}
