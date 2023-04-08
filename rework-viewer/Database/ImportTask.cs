using osu.Framework.Extensions;
using rework_viewer.IO.Archives;

namespace rework_viewer.Database;

/// <summary>
/// An encapsulated import task to be imported to an <see cref="RealmArchiveModelImporter{TModel}"/>
/// </summary>
public class ImportTask
{
    /// <summary>
    /// The path to the file (or filename in the case a stream is provided).
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// An optional stream which provides the file content.
    /// </summary>
    public Stream? Stream { get; }

    /// <summary>
    /// Constructs a new import task from a path (on a local filesystem).
    /// </summary>
    public ImportTask(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Constructs a new import task from a stream. The provided stream will be disposed after reading.
    /// </summary>
    public ImportTask(Stream stream, string filename)
    {
        Path = filename;
        Stream = stream;
    }

    public ArchiveReader GetReader()
    {
        return Stream != null
                   ? getReaderFrom(Stream)
                   : getReaderFrom(Path);
    }

    public virtual void DeleteFile()
    {
        if (File.Exists(Path))
            File.Delete(Path);
    }

    private ArchiveReader getReaderFrom(Stream stream)
    {
        if (!(stream is MemoryStream memoryStream))
        {
            // This isn't used in any current path. May need to reconsider for performance reasons (ie. if we don't expect the incoming stream to be copied out).
            memoryStream = new MemoryStream(stream.ReadAllBytesToArray());
            stream.Dispose();
        }

        return new ByteArrayReader(memoryStream.ToArray(), Path);
    }

    private ArchiveReader getReaderFrom(string path)
    {
        if (Directory.Exists(path))
            return new DirectoryArchiveReader(path);
        if (File.Exists(path))
            return new FileArchiveReader(path);

        throw new InvalidOperationException($"{path} is not a valid archive.");
    }

    public override string ToString() => System.IO.Path.GetFileName(Path);
}
