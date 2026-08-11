using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

public interface IPickedService {
    Task SyncToPickedAsync(Picture picture);
    Task SyncToHighlightAsync(Picture picture);
}
