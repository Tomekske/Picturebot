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
}
