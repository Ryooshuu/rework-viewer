namespace rework_viewer.IO.Archives;

public class DirectoryArchiveReader : ArchiveReader
{
    private readonly string path;

    public DirectoryArchiveReader(string path)
        : base(Path.GetFileName(path))
    {
        // re-get full path to standardise with Directory.GetFiles return values below.
        this.path = Path.GetFullPath(path);
    }

    public override Stream? GetStream(string name)
        => File.OpenRead(Path.Combine(path, name));

    public override void Dispose()
    {
    }

    public override IEnumerable<string> Filenames
        => Directory.GetFiles(path, "*", SearchOption.AllDirectories)
           .Select(f => f.Replace(path, string.Empty).Trim(Path.DirectorySeparatorChar))
           .ToArray();
}
