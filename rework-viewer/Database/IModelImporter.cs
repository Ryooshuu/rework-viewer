namespace rework_viewer.Database;

/// <summary>
/// A class which handles importing of associated models to the game store.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IModelImporter<TModel> : ICanAcceptFiles
    where TModel : class, IHasGuidPrimaryKey
{
    /// <summary>
    /// Process multiple import tasks, updating a tracking notification with progress.
    /// </summary>
    /// <param name="tasks">The import tasks</param>
    /// <param name="isBatch">Whether this import is part of a larger batch.</param>
    /// <param name="obj"></param>
    /// <returns>The imported models.</returns>
    Task<IEnumerable<Live<TModel>>> Import(ImportTask[] tasks, bool isBatch = false, object? obj = null);

    /// <summary>
    /// Processes a single import as an update for an existing model.
    /// This will still run a full import, but perform any post-processing required to make it feel like an update to the user.
    /// </summary>
    /// <param name="task">The import task.</param>
    /// <param name="original">The original model which is being updated.</param>
    /// <returns>The imported model.</returns>
    Task<Live<TModel>?> ImportAsUpdate(ImportTask task, TModel original);

    /// <summary>
    /// A user displayable name for the model type associated with this manager.
    /// </summary>
    string HumanisedModelName => $"{typeof(TModel).Name.Replace(@"Info", "").ToLowerInvariant()}";
}
