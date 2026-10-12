using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Models;
using ErrorOr;

namespace PictureWorker.Domain.Interfaces;

/// <summary>
///     Service for evaluating photos in a burst or sequence using multimodal AI vision against strict curation criteria.
/// </summary>
public interface IAiPictureCurationService {
    /// <summary>
    ///     Evaluates a group/burst of pictures, choosing exactly one best picture and generating diagnostic feedback per item.
    /// </summary>
    /// <param name="pictures">List of tuples containing unique picture identifier and image file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AiCurationResult with the best pick ID and per-image feedback dictionary.</returns>
    Task<ErrorOr<AiCurationResult>> EvaluateBurstAsync(
        IReadOnlyList<(string Id, string ImagePath)> pictures,
        CancellationToken cancellationToken = default);
}
