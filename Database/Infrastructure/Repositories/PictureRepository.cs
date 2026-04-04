using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Database.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Database.Infrastructure.Repositories;

public class PictureRepository(ApplicationDbContext context) : NodeRepository(context), IPictureRepository {
    public async Task<List<Picture>> FindByHierarchyIdAsync(int hierarchyId) {
        return await context.Nodes
            .OfType<Picture>()
            .Include(p => p.Metrics)
            .Where(p => p.ParentId == hierarchyId)
            .AsNoTracking()
            .ToListAsync();
    }
}
