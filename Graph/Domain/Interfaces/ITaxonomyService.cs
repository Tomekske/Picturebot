using System.Collections.Generic;

namespace Graph.Domain.Interfaces;

/// <summary>
///     Service for expanding leaf tags into ancestor taxonomy chains and hierarchical XMP paths.
/// </summary>
public interface ITaxonomyService {
    /// <summary>
    ///     Gets the ancestor names for a leaf tag (e.g. ["Animals", "Mammals"] for leaf "Dog").
    /// </summary>
    List<string> GetAncestorChain(string leafTagName);

    /// <summary>
    ///     Gets the full pipe-delimited hierarchical path for a leaf tag (e.g. "Animals|Mammals|Dog").
    /// </summary>
    string GetFullHierarchicalPath(string leafTagName);

    /// <summary>
    ///     Resolves all flat dc:subject tags including ancestors and the leaf tag itself (e.g. ["Animals", "Mammals", "Dog"]).
    /// </summary>
    HashSet<string> ResolveTaxonomySubjectChain(string leafTagName);
}
