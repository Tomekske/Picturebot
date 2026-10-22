using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

public class NodeRepository(ApplicationDbContext context) : INodeRepository {
    protected readonly ApplicationDbContext _context = context;

    public async Task CreateAsync(Node node) {
        var strayMetrics = _context.ChangeTracker.Entries<Metrics>()
            .Where(e => e.State == EntityState.Added && (node is not Picture p || e.Entity != p.Metrics))
            .ToList();
        foreach (var entry in strayMetrics) {
            entry.State = EntityState.Detached;
        }

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

    public async Task<Node?> FindByIdReadOnlyAsync(int id) {
        return await _context.Nodes
            .AsNoTracking()
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
            .AsNoTracking()
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
        var picture = node as Picture;
        var metrics = picture?.Metrics;

        node.Parent = null;
        node.Children = null;
        if (picture != null) {
            picture.Metrics = null;
        }

        try {
            // Clean up any stray Added metrics before starting to avoid SQLite Error 19
            var strayMetricsBefore = _context.ChangeTracker.Entries<Metrics>()
                .Where(e => e.State == EntityState.Added)
                .ToList();
            foreach (var entry in strayMetricsBefore) {
                entry.State = EntityState.Detached;
            }

            if (trackedEntity != null) {
                // Update the properties of the tracked entity without replacing it
                _context.Entry(trackedEntity).CurrentValues.SetValues(node);
            } else {
                // Attach the node and mark as modified
                _context.Nodes.Attach(node);
                _context.Entry(node).State = EntityState.Modified;
            }

            if (metrics != null && node.Id > 0) {
                var existingMetrics = _context.Metrics.Local.FirstOrDefault(m => m.PictureId == node.Id)
                    ?? await _context.Metrics.FirstOrDefaultAsync(m => m.PictureId == node.Id);
                if (existingMetrics != null) {
                    _context.Entry(existingMetrics).CurrentValues.SetValues(metrics);
                } else {
                    _context.Metrics.Add(new Metrics {
                        PictureId = node.Id,
                        Sharpness = metrics.Sharpness,
                        PHash = metrics.PHash,
                        Embedding = metrics.Embedding,
                        AiFeedback = metrics.AiFeedback
                    });
                }
            }
            
            await _context.SaveChangesAsync();
        } finally {
            // Clean up any stray Added metrics after save
            var strayMetricsAfter = _context.ChangeTracker.Entries<Metrics>()
                .Where(e => e.State == EntityState.Added)
                .ToList();
            foreach (var entry in strayMetricsAfter) {
                entry.State = EntityState.Detached;
            }

            node.Parent = parent;
            node.Children = children;
            if (picture != null) {
                picture.Metrics = metrics;
            }

            if (trackedEntity == null) {
                _context.Entry(node).State = EntityState.Detached;
            }

            if (metrics != null) {
                var mEntry = _context.Entry(metrics);
                if (mEntry != null && mEntry.State == EntityState.Added) {
                    mEntry.State = EntityState.Detached;
                }
            }
        }
    }

    public async Task DeleteAsync(Node node) {
        if (node.Id <= 0) return;

        if (node is Album) {
            // 1. Delete all metrics of children pictures
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM metrics WHERE PictureId IN (SELECT Id FROM nodes WHERE ParentId = {node.Id});");

            // 2. Delete from pictures table
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM pictures WHERE Id IN (SELECT Id FROM nodes WHERE ParentId = {node.Id});");

            // 3. Delete from nodes table (for children pictures)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM nodes WHERE ParentId = {node.Id};");

            // 4. Delete from albums table
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM albums WHERE Id = {node.Id};");

            // 5. Delete from nodes table (for the album itself)
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM nodes WHERE Id = {node.Id};");
        } else if (node is Picture) {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM metrics WHERE PictureId = {node.Id};");

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM pictures WHERE Id = {node.Id};");

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM nodes WHERE Id = {node.Id};");
        } else if (node is Folder) {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM folders WHERE Id = {node.Id};");

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM nodes WHERE Id = {node.Id};");
        } else {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM nodes WHERE Id = {node.Id};");
        }

        // Clean up any locally tracked entities in EF Core change tracker
        var localNodes = _context.Nodes.Local
            .Where(n => n.Id == node.Id || (node is Album && n.ParentId == node.Id))
            .ToList();
        foreach (var local in localNodes) {
            _context.Entry(local).State = EntityState.Detached;
        }

        var localMetrics = _context.Metrics.Local
            .Where(m => m.PictureId == node.Id || localNodes.Any(ln => ln.Id == m.PictureId))
            .ToList();
        foreach (var local in localMetrics) {
            _context.Entry(local).State = EntityState.Detached;
        }

        // Clean up any stray Added metrics that might have been queued in the change tracker
        var strayMetrics = _context.ChangeTracker.Entries<Metrics>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        foreach (var entry in strayMetrics) {
            entry.State = EntityState.Detached;
        }
    }
}
