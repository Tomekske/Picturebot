using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

public interface ICurationQueue {
    void Enqueue(Picture picture);
    int Count { get; }
}
