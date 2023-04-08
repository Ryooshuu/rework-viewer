namespace rework_viewer.IO.Archives;

public class FileArchiveReader : ArchiveReader
{
    private readonly string path;
    
    public FileArchiveReader(string path)
        : base(Path.GetFileName(path))
    {
        // re-get full path to standardise
        this.path = Path.GetFullPath(path);
    }

    public override Stream? GetStream(string name)
        => File.OpenRead(path);

    public override void Dispose()
    {
    }

    public override IEnumerable<string> Filenames
        => new[] { Name };
}
