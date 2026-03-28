using Database.Domain.Entities;
using Database.Domain.Interfaces;
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

        await nodeRepository.CreateAsync(node);
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
            } else {
                roots.Add(node);
            }
        }

        return roots;
    }

    public async Task<List<Node>> GetAllNodesAsync() {
        return await nodeRepository.FindAllAsync();
    }
}
