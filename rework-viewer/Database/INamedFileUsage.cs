using rework_viewer.IO;

namespace rework_viewer.Database;

/// <summary>
/// A usage of a file, with a local filename attached.
/// </summary>
public interface INamedFileUsage
{
    /// <summary>
    /// The underlying file on disk.
    /// </summary>
    IFileInfo File { get; }
    
    /// <summary>
    /// The filename for this usage.
    /// </summary>
    string Filename { get; }
}
