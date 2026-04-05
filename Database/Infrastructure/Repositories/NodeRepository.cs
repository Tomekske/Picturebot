using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

public class NodeRepository(ApplicationDbContext context) : INodeRepository {
    public async Task CreateAsync(Node node) {
        context.Nodes.Add(node);
        await context.SaveChangesAsync();
    }

    public async Task<List<Node>> FindAllAsync() {
        return await context.Nodes
            .AsNoTracking()
            .Include(n => (n as Picture)!.Metrics)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }

    public async Task<Node?> FindByIdAsync(int id) {
        return await context.Nodes
            .Include(n => n.Parent)
            .Include(n => (n as Picture)!.Metrics)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<bool> FindDuplicateAsync(int? parentId, string name, NodeType type) {
        var query = context.Nodes.AsQueryable();

        query = parentId == null
            ? query.Where(n => n.ParentId == null)
            : query.Where(n => n.ParentId == parentId);

        return await query.AnyAsync(n => n.Name == name && n.Type == type);
    }

    public async Task<List<Node>> FindNodesByTypeAsync(NodeType type) {
        return await context.Nodes
            .AsNoTracking()
            .Include(n => n.Parent)
            .Include(n => (n as Picture)!.Metrics)
            .Where(n => n.Type == type)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }

    public async Task<List<Node>> FindChildrenAsync(int parentId) {
        return await context.Nodes
            .Include(n => n.Parent)
            .Include(n => (n as Picture)!.Metrics)
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }


    public async Task UpdateAsync(Node node) {
        var trackedEntity = context.Nodes.Local.FirstOrDefault(n => n.Id == node.Id);
        if (trackedEntity != null) {
            context.Entry(trackedEntity).State = EntityState.Detached;
        }
        
        context.Nodes.Update(node);
        await context.SaveChangesAsync();
    }
}
