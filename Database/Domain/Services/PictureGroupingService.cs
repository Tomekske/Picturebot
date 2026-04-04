using Database.Domain.Entities;
using Database.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace Database.Domain.Services;

/// <summary>
///     Implements perceptual hash grouping logic using Hamming distance.
/// </summary>
public class PictureGroupingService(IPictureRepository repository, ILogger<PictureGroupingService> logger) : IPictureGroupingService {
    public async Task<List<List<Picture>>> GroupSimilarPicturesAsync(int hierarchyId, int threshold) {
        logger.LogInformation("Grouping pictures for hierarchy {HierarchyId} with threshold {Threshold}", hierarchyId, threshold);
        
        var pictures = await repository.FindByHierarchyIdAsync(hierarchyId);
        var groups = new List<List<Picture>>();

        foreach (var picture in pictures) {
            if (picture.Metrics?.PHash == null) {
                continue;
            }

            ulong currentHash = picture.Metrics.PHash.Value;
            bool addedToGroup = false;

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

        logger.LogInformation("Formed {GroupCount} groups for hierarchy {HierarchyId}", groups.Count, hierarchyId);
        return groups;
    }

    private bool IsSimilarToAllMembers(ulong hash, List<Picture> group, int threshold) {
        foreach (var member in group) {
            // Member must have pHash here since they are in a group
            if (HammingDistance(hash, member.Metrics!.PHash!.Value) > threshold) {
                return false;
            }
        }
        return true;
    }

    private int HammingDistance(ulong h1, ulong h2) {
        return BitOperations.PopCount(h1 ^ h2);
    }
}
