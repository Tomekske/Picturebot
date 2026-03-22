namespace Domain.Enums;

/// <summary>
///     Defines the possible types of nodes within the hierarchy.
/// </summary>
public enum NodeType
{
    /// <summary>
    ///     A logical organizational container.
    /// </summary>
    Folder = 0,

    /// <summary>
    ///     A physical container that maps to a directory and contains pictures.
    /// </summary>
    Album = 1,

    /// <summary>
    ///     An individual picture file with associated metadata.
    /// </summary>
    Picture = 2
}
