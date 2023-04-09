using osu.Framework.Platform;
using Realms;
using rework_viewer.Database;
using rework_viewer.IO.Archives;

namespace rework_viewer.Reworks;

public class ReworkImporter : RealmArchiveModelImporter<RealmRework>
{
    public override IEnumerable<string> HandledExtensions => new[] { ".dll" };
    protected override string[] HashableFileTypes => HandledExtensions.ToArray();
    public Action<(RealmRework rework, bool isBatch)>? ProcessRework { private get; set; }
    public ReworkImporter(Storage storage, RealmAccess realm)
        : base(storage, realm)
    {
    }

    protected override RealmRework? CreateModel(ArchiveReader reader)
    {
        return new RealmRework { DateAdded = DateTimeOffset.UtcNow };
    }

    protected override void Populate(RealmRework model, ArchiveReader? archive, Realm realm, CancellationToken cancellationToken = default)
    {
        // We are guaranteed to only have the DLL file in the archive.
        var dllFile = model.Files.First(f => f.Filename == archive!.Name);
        var type = Enum.Parse<RulesetType>(archive!.Name.Split('.').Take(archive.Name.Split('.').Length - 1).Last());

        model.DllFile = dllFile.Filename;
        model.Hash = dllFile.File.Hash;
        model.Ruleset = realm.All<RealmRuleset>().First(r => r.TypeInt == (int) type).Detach();
    }
}
