using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

public class PictureRepository(ApplicationDbContext context) : NodeRepository(context), IPictureRepository {
    public async Task<List<Picture>> FindByHierarchyIdAsync(int hierarchyId) {
        return await _context.Nodes
            .OfType<Picture>()
            .Include(p => p.Metrics)
            .Where(p => p.ParentId == hierarchyId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Picture>> SearchGlobalAsync(string query, CancellationToken cancellationToken = default) {
        var queryable = _context.Nodes
            .OfType<Picture>()
            .Include(p => p.Metrics)
            .Include(p => p.Parent)
            .AsNoTracking();

        if (string.IsNullOrWhiteSpace(query)) {
            return await queryable.ToListAsync(cancellationToken);
        }

        var trimmed = query.Trim();
        var normalized = trimmed.Replace(" › ", "|")
                                .Replace(" > ", "|")
                                .Replace("›", "|")
                                .Replace(">", "|")
                                .Replace('/', '|')
                                .Replace('\\', '|')
                                .Trim('|')
                                .Trim();

        var likePattern = $"%{trimmed}%";
        var normPattern = $"%{normalized}%";

        return await queryable
            .Where(p => (p.KeywordsJson != null && (EF.Functions.Like(p.KeywordsJson, likePattern) || EF.Functions.Like(p.KeywordsJson, normPattern) || p.KeywordsJson.ToLower().Contains(trimmed.ToLower()) || p.KeywordsJson.ToLower().Contains(normalized.ToLower())))
                     || (p.Name != null && (EF.Functions.Like(p.Name, likePattern) || EF.Functions.Like(p.Name, normPattern) || p.Name.ToLower().Contains(trimmed.ToLower())))
                     || (p.Parent != null && p.Parent.Name != null && (EF.Functions.Like(p.Parent.Name, likePattern) || p.Parent.Name.ToLower().Contains(trimmed.ToLower()))))
            .ToListAsync(cancellationToken);
    }
}
