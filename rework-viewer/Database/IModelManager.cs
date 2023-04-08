namespace rework_viewer.Database;

/// <summary>
/// Represents a model manager that publishes events when <typeparamref name="TModel"/> are added or removed.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public interface IModelManager<TModel>
    where TModel : class
{
    /// <summary>
    /// Delete an item from the manager.
    /// Is a no-op for already deleted items.
    /// </summary>
    /// <param name="item">The item to delete.</param>
    /// <returns>false if no operation was performed.</returns>
    bool Delete(TModel item);

    /// <summary>
    /// Delete multiple items.
    /// </summary>
    void Delete(List<TModel> items);

    /// <summary>
    /// Restore multiple items that were previously marked as deleted.
    /// <remarks>
    /// Is a no-op if the item is deleted from the database after being marked for deletion for an hour.
    /// </remarks>
    /// </summary>
    void Undelete(List<TModel> items);

    /// <summary>
    /// Restore an item that was previously marked as deleted.
    /// Is a no-op if the item is not in a deleted state, or has its protected flag set.
    /// <remarks>
    /// Is a no-op if the item is deleted from the database after being marked for deletion for an hour.
    /// </remarks>
    /// </summary>
    void Undelete(TModel model);

    /// <summary>
    /// Checks whether a given <typeparamref name="TModel"/> is already available in the local store.
    /// </summary>
    /// <param name="model">The <typeparamref name="TModel"/> whose existence needs to be checked.</param>
    /// <returns>Whether the <typeparamref name="TModel"/> exists.</returns>
    bool IsAvailableLocally(TModel model);
}
