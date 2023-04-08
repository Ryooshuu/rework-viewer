namespace rework_viewer.Database;

/// <summary>
/// A model that can be deleted from user's view without being instantly lost.
/// The time for deletion is an hour after the model has been marked for deletion,
/// unless if specified explicitly.
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// Whether this model is marked for future deletion.
    /// </summary>
    bool DeletePending { get; set; }
}
