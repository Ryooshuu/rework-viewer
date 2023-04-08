namespace rework_viewer.IO;

/// <summary>
/// A representation of a tracked file.
/// </summary>
public interface IFileInfo
{
    /// <summary>
    /// SHA-256 hash of the file content.
    /// </summary>
    string Hash { get; }
}
