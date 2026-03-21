namespace Domain.Enums;

/// <summary>
///     Defines the possible types of nodes within the hierarchy.
/// </summary>
public enum NodeType
{
    /// <summary>
    ///     A logical organizational container.
    /// </summary>
    Folder,

    /// <summary>
    ///     A physical container that maps to a directory and contains pictures.
    /// </summary>
    Album,

    /// <summary>
    ///     An individual picture file with associated metadata.
    /// </summary>
    Picture
}
