namespace rework_viewer.Database;

public interface IModelFileManager<in TModel, in TFileModel>
    where TModel : class
    where TFileModel : class
{
    /// <summary>
    /// Replace an existing file with a new version.
    /// </summary>
    /// <param name="model">The item to operate on.</param>
    /// <param name="file">The existing file to be replaced.</param>
    /// <param name="contents">The new file contents.</param>
    void ReplaceFile(TModel model, TFileModel file, Stream contents);

    /// <summary>
    /// Delete an existing file.
    /// </summary>
    /// <param name="model">The item to operate on.</param>
    /// <param name="file">The existing file to be deleted.</param>
    void DeleteFile(TModel model, TFileModel file);

    /// <summary>
    /// Adds a new file. If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="model">The item to operate on.</param>
    /// <param name="contents">The new file contents.</param>
    /// <param name="filename">The filename for the new file.</param>
    void AddFile(TModel model, Stream contents, string filename);
}
