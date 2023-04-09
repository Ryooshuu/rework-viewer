using System.Diagnostics;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using rework_viewer.Database;
using rework_viewer.IO.Archives;

namespace rework_viewer.Reworks;

[ExcludeFromDynamicCompile]
public class ReworkManager : ModelManager<RealmRework>, IModelImporter<RealmRework>
{
    private readonly RealmFileStore fileStore;
    private readonly ReworkImporter reworkImporter;
    private readonly RulesetCache rulesetCache;

    public ReworkManager(Storage storage, RealmAccess realm)
        : base(storage, realm)
    {
        fileStore = new RealmFileStore(realm, storage);
        reworkImporter = new ReworkImporter(storage, realm);
        rulesetCache = new RulesetCache(fileStore.Store);
    }

    public Task<Live<RealmRework>?> ImportAsUpdate(ImportTask task, RealmRework original)
        => reworkImporter.ImportAsUpdate(task, original);

    #region Implementation of ICanAcceptFiles

    public Task Import(params string[] paths)
        => reworkImporter.Import(paths);

    public Task Import(ImportTask[] tasks, bool isBatch = false)
        => reworkImporter.Import(tasks, isBatch);

    public Task<IEnumerable<Live<RealmRework>>> Import(ImportTask[] tasks, bool isBatch = false, object? obj = null)
        => reworkImporter.Import(tasks, isBatch, obj);

    public Live<RealmRework>? Import(RealmRework item, ArchiveReader? archive = null, CancellationToken cancellationToken = default)
        => reworkImporter.ImportModel(item, archive, false, cancellationToken);

    public IEnumerable<string> HandledExtensions => reworkImporter.HandledExtensions;

    #endregion

    public Ruleset GetRuleset(RealmRework rework, bool refetch = false)
    {
        if (refetch)
            rulesetCache.Invalidate(rework);

        var missingFiles = rework.Files.Count == 0;

        if (refetch || rework.IsManaged || missingFiles)
        {
            var id = rework.ID;
            rework = Realm.Realm.Find<RealmRework>(id)?.Detach() ?? rework;
        }
        
        Debug.Assert(rework.IsManaged != true);
        
        return rulesetCache.GetRuleset(rework);
    }

    public void Invalidate(RealmRework rework)
        => rulesetCache.Invalidate(rework);

    public override bool IsAvailableLocally(RealmRework model)
        => Realm.Run(realm => realm.All<RealmRework>().Any(s => s.ID == model.ID));

    public Action<IEnumerable<Live<RealmRework>>>? PresentImport
    {
        set => reworkImporter.PresentImport = value;
    }

    public override string HumanisedModelName => "rework";
}
