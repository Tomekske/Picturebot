using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

public class NodeRepository(ApplicationDbContext context) : INodeRepository {
    protected readonly ApplicationDbContext _context = context;

    public async Task CreateAsync(Node node) {
        _context.Nodes.Add(node);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Node>> FindAllAsync() {
        return await _context.Nodes
            .AsNoTracking()
            .Include(n => (n as Picture)!.Metrics)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }

    public async Task<Node?> FindByIdAsync(int id) {
        return await _context.Nodes
            .Include(n => n.Parent)
            .Include(n => (n as Picture)!.Metrics)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<bool> FindDuplicateAsync(int? parentId, string name, NodeType type) {
        var query = _context.Nodes.AsQueryable();

        query = parentId == null
            ? query.Where(n => n.ParentId == null)
            : query.Where(n => n.ParentId == parentId);

        return await query.AnyAsync(n => n.Name == name && n.Type == type);
    }

    public async Task<bool> IsPictureHashDuplicateAsync(int parentId, ulong hash) {
        return await _context.Nodes.OfType<Picture>()
            .AnyAsync(p => p.ParentId == parentId && p.Hash == hash);
    }

    public async Task<List<Node>> FindNodesByTypeAsync(NodeType type) {
        return await _context.Nodes
            .AsNoTracking()
            .Include(n => n.Parent)
            .Include(n => (n as Picture)!.Metrics)
            .Where(n => n.Type == type)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }

    public async Task<List<Node>> FindChildrenAsync(int parentId) {
        return await _context.Nodes
            .Include(n => n.Parent)
            .Include(n => (n as Picture)!.Metrics)
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.Name)
            .ToListAsync();
    }


    public async Task UpdateAsync(Node node) {
        var trackedEntity = _context.Nodes.Local.FirstOrDefault(n => n.Id == node.Id);
        
        var parent = node.Parent;
        var children = node.Children;
        node.Parent = null;
        node.Children = null;

        try {
            if (trackedEntity != null) {
                // Update the properties of the tracked entity without replacing it
                _context.Entry(trackedEntity).CurrentValues.SetValues(node);
            } else {
                // Attach the node and mark as modified
                _context.Nodes.Attach(node);
                _context.Entry(node).State = EntityState.Modified;
            }
            
            await _context.SaveChangesAsync();
        } finally {
            node.Parent = parent;
            node.Children = children;
        }
    }

    public async Task DeleteAsync(Node node) {
        var trackedEntity = _context.Nodes.Local.FirstOrDefault(n => n.Id == node.Id);

        if (node is Album) {
            var children = await _context.Nodes
                .Where(n => n.ParentId == node.Id)
                .ToListAsync();
            _context.Nodes.RemoveRange(children);
        }

        if (trackedEntity != null) {
            _context.Nodes.Remove(trackedEntity);
        } else {
            // Detach navigation properties to prevent EF from trying to track the entire graph,
            // which causes identity conflicts if the parent or children are already tracked.
            node.Parent = null;
            node.Children = null;
            _context.Nodes.Remove(node);
        }

        await _context.SaveChangesAsync();
    }
}
