namespace rework_viewer.Database;

public interface IHasNamedFiles
{
    /// <summary>
    /// All files used by this model.
    /// </summary>
    IEnumerable<INamedFileUsage> Files { get; }
}
