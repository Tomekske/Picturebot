using Database.Domain.Entities;

namespace Graph.Domain.Interfaces;

public interface ICopyService {
    Task<bool> CopyToEditAsync(Picture picture);
    Task<bool> CopyToPrintAsync(Picture picture);
}
