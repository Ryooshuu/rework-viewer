using JetBrains.Annotations;
using osu.Framework.Testing;
using Realms;
using rework_viewer.Database;
using rework_viewer.IO;

namespace rework_viewer.Models;

[ExcludeFromDynamicCompile]
public class RealmNamedFileUsage : EmbeddedObject, INamedFile, INamedFileUsage
{
    public RealmFile File { get; set; } = null!;

    public string Filename { get; set; } = null!;

    public RealmNamedFileUsage(RealmFile file, string filename)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
        Filename = filename ?? throw new ArgumentNullException(nameof(filename));
    }

    [UsedImplicitly]
    private RealmNamedFileUsage()
    {
    }

    IFileInfo INamedFileUsage.File => File;
}
