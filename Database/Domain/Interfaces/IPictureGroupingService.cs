using Database.Domain.Entities;

namespace Database.Domain.Interfaces;

/// <summary>
///     Defines the contract for grouping visually similar pictures using perceptual hashing.
/// </summary>
public interface IPictureGroupingService {
    /// <summary>
    ///     Groups pictures in a specific hierarchy by their visual similarity.
    /// </summary>
    /// <param name="hierarchyId">The unique identifier of the parent node.</param>
    /// <param name="threshold">The maximum Hamming distance between picture hashes for them to be considered similar.</param>
    /// <returns>A task that represents the asynchronous operation, returning a list of picture groups.</returns>
    Task<List<List<Picture>>> GroupSimilarPicturesAsync(int hierarchyId, int threshold);
}
