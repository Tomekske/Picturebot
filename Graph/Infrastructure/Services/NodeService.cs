using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Graph.Domain.Strategies;

namespace Graph.Infrastructure.Services;

public class NodeService(
    INodeRepository nodeRepository,
    NodeStrategyFactory strategyFactory) : INodeService {
    public async Task CreateNodeAsync(Node node) {
        var strategy = strategyFactory.GetStrategy(node.Type);

        Node? parent = null;
        if (node.ParentId.HasValue) {
            parent = await nodeRepository.FindByIdAsync(node.ParentId.Value);
            if (parent == null) {
                throw new InvalidOperationException($"Parent with ID {node.ParentId.Value} not found.");
            }
        }

        await strategy.ValidateAsync(node, parent);
        strategy.Prepare(node);
        node.Parent = parent; // Ensure breadcrumbs work for the returned object

        await nodeRepository.CreateAsync(node);
    }

    public async Task<bool> IsPictureHashDuplicateAsync(int parentId, ulong hash) {
        return await nodeRepository.IsPictureHashDuplicateAsync(parentId, hash);
    }

    public async Task<bool> ExistsAsync(int? parentId, string name, NodeType type) {
        return await nodeRepository.FindDuplicateAsync(parentId, name, type);
    }

    public async Task<List<Node>> LoadHydratedTreeAsync() {
        var allNodes = await nodeRepository.FindAllAsync();

        // Build the tree in memory
        var nodeMap = allNodes.ToDictionary(n => n.Id);
        var roots = new List<Node>();

        foreach (var node in allNodes) {
            if (node.ParentId.HasValue && nodeMap.TryGetValue(node.ParentId.Value, out var parent)) {
                parent.Children ??= new List<Node>();
                parent.Children.Add(node);
                node.Parent = parent; // Set the parent reference
            } else {
                roots.Add(node);
            }
        }

        return roots;
    }

    public async Task<List<Node>> GetAllNodesAsync() {
        return await nodeRepository.FindAllAsync();
    }

    public async Task<List<Node>> FindChildrenAsync(int parentId) {
        return await nodeRepository.FindChildrenAsync(parentId);
    }

    public async Task UpdateNodeAsync(Node node) {
        await nodeRepository.UpdateAsync(node);
    }

    public async Task DeleteNodeAsync(Node node) {
        await nodeRepository.DeleteAsync(node);
    }
}
