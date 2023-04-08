using rework_viewer.Database;
using rework_viewer.IO;
using rework_viewer.Models;

namespace rework_viewer.Extensions;

public static class ModelExtensions
{
    /// <summary>
    /// Returns the file usage for the file in this model with the given filename, if any exists, otherwise null.
    /// The path returned is relative to the user file storage.
    /// The lookup is case insensitive.
    /// </summary>
    /// <param name="model">The model to operate on.</param>
    /// <param name="filename">The name of the file to get the storage path of.</param>
    public static RealmNamedFileUsage? GetFile(this IHasRealmFiles model, string filename)
        => model.Files.SingleOrDefault(f => string.Equals(f.Filename, filename, StringComparison.OrdinalIgnoreCase));
    
    public static string GetStoragePath(this IFileInfo fileInfo)
        => Path.Combine(fileInfo.Hash.Remove(1), fileInfo.Hash.Remove(2), fileInfo.Hash);
}
