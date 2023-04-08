namespace rework_viewer.Database;

/// <summary>
/// A class which can accept files for importing.
/// </summary>
public interface ICanAcceptFiles
{
    /// <summary>
    /// Import one or more items from filesystem <paramref name="paths"/>
    /// </summary>
    /// <remarks>
    /// This will be treated as a low priority batch import if more than one path is specified.
    /// This will post notifications tracking progress.
    /// </remarks>
    /// <param name="paths">The files which should be imported.</param>
    Task Import(params string[] paths);
    
    /// <summary>
    /// Import the specified files from the given import tasks
    /// </summary>
    /// <remarks>
    /// This will be treated as a low priority batch import if more than one path is specified.
    /// This will post notifications tracking progress.
    /// </remarks>
    /// <param name="tasks">The import tasks from which the files should be imported.</param>
    /// <param name="isBatch">
    /// Whether this import is part of a larger batch.
    /// <remarks>
    /// May skip intensive pre-import checks in favour of faster processing.
    ///
    /// More specifically, imports will be skipped before they begin, given an existing model matches on hash and filenames.
    /// Should generally only be used for large batch imports, as it may defy user expectations when updating an existing model.
    ///
    /// Will also change scheduling behaviour to run at a lower priority.
    /// </remarks>
    /// </param>
    Task Import(ImportTask[] tasks, bool isBatch = false);
    
    /// <summary>
    /// An array of accepted file extensions (in the standard format of ".abc").
    /// </summary>
    IEnumerable<string> HandledExtensions { get; }
}
