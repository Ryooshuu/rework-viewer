using rework_viewer.Models;

namespace rework_viewer.Database;

/// <summary>
/// A model that contains a list of files it is responsible for.
/// </summary>
public interface IHasRealmFiles
{
    /// <summary>
    /// Available files in this model, with locally filenames.
    /// </summary>
    IList<RealmNamedFileUsage> Files { get; }
    
    /// <summary>
    /// A combined hash representing the model, based on the files it contains.
    /// Implementation specific.
    /// </summary>
    string Hash { get; set; }
}
