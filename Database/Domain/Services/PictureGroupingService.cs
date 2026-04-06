using System.Numerics;
using Database.Domain.Entities;
using Database.Domain.Interfaces;

namespace Database.Domain.Services;

/// <summary>
///     Implements perceptual hash grouping logic using Hamming distance.
/// </summary>
public class PictureGroupingService(IPictureRepository repository) : IPictureGroupingService {
    public async Task<List<List<Picture>>> GroupSimilarPicturesAsync(int hierarchyId, int threshold) {
        var pictures = await repository.FindByHierarchyIdAsync(hierarchyId);
        var groups = new List<List<Picture>>();

        foreach (var picture in pictures) {
            if (picture.Hash == 0) {
                continue;
            }

            var currentHash = picture.Hash;
            var addedToGroup = false;

            foreach (var group in groups) {
                if (IsSimilarToAllMembers(currentHash, group, threshold)) {
                    group.Add(picture);
                    addedToGroup = true;
                    break;
                }
            }

            if (!addedToGroup) {
                groups.Add(new List<Picture> { picture });
            }
        }

        return groups;
    }

    private bool IsSimilarToAllMembers(ulong hash, List<Picture> group, int threshold) {
        foreach (var member in group) {
            if (HammingDistance(hash, member.Hash) > threshold) {
                return false;
            }
        }

        return true;
    }

    private int HammingDistance(ulong h1, ulong h2) {
        return BitOperations.PopCount(h1 ^ h2);
    }
}
