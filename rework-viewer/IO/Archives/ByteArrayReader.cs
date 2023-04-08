namespace rework_viewer.IO.Archives;

public class ByteArrayReader : ArchiveReader
{
    private readonly byte[] content;
    
    public ByteArrayReader(byte[] content, string filename)
        : base(filename)
    {
        this.content = content;
    }

    public override Stream? GetStream(string name)
        => new MemoryStream(content);

    public override void Dispose()
    {
    }

    public override IEnumerable<string> Filenames
        => new[] { Name };
}
