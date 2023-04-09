using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Extensions;
using Realms;
using rework_viewer.Extensions;
using rework_viewer.IO.Archives;
using rework_viewer.Models;

namespace rework_viewer.Database;

/// <summary>
/// Encapsulates a model store class to give it import functionality.
/// Adds cross-functionality with <see cref="RealmFileStore"/> to give access to the central file store for the provided model.
/// </summary>
public abstract class RealmArchiveModelImporter<TModel> : IModelImporter<TModel>
    where TModel : RealmObject, IHasRealmFiles, IHasGuidPrimaryKey, ISoftDelete
{
    /// <summary>
    /// The maximum number of concurrent imports to run per import scheduler.
    /// </summary>
    private const int import_queue_request_concurrency = 1;

    /// <summary>
    /// The minimum number of items in a single import call in order for the import to be processed as a batch.
    /// Batch imports will apply optimisations preferring speed over consistency when detecting changes in already-imported items.
    /// </summary>
    private const int minimum_items_considered_batch_import = 10;

    /// <summary>
    /// A singleton scheduler shared by all <see cref="RealmArchiveModelImporter{TModel}"/>.
    /// </summary>
    /// <remarks>
    /// This scheduler generally performs IO and CPU intensive work so concurrency is limited harshly.
    /// It is mainly being used as a queue mechanism for large imports.
    /// </remarks>
    private static readonly ThreadedTaskScheduler import_scheduler = new(import_queue_request_concurrency, nameof(RealmArchiveModelImporter<TModel>));

    /// <summary>
    /// A second scheduler for batch imports.
    /// For simplicity, these will just run in parallel with normal priority imports, but a future refactor would see this implemented via a custom scheduler/queue.
    /// See https://gist.github.com/peppy/f0e118a14751fc832ca30dd48ba3876b for an incomplete version of this.
    /// </summary>
    private static readonly ThreadedTaskScheduler import_scheduler_batch = new(import_queue_request_concurrency, nameof(RealmArchiveModelImporter<TModel>));


    public abstract IEnumerable<string> HandledExtensions { get; }

    protected readonly RealmFileStore Files;

    protected readonly RealmAccess Realm;

    /// <summary>
    /// Fired when the user requests to view the resulting import.
    /// </summary>
    public Action<IEnumerable<Live<TModel>>>? PresentImport { get; set; }

    protected RealmArchiveModelImporter(Storage storage, RealmAccess realm)
    {
        Realm = realm;
        Files = new RealmFileStore(realm, storage);
    }

    public Task Import(params string[] paths)
        => Import(paths.Select(p => new ImportTask(p)).ToArray(), default, new { });

    public Task Import(ImportTask[] tasks, bool isBatch = false)
        => Import(tasks, isBatch, new { });

    public async Task<IEnumerable<Live<TModel>>> Import(ImportTask[] tasks, bool isBatch = false, object? obj = null!)
    {
        if (tasks.Length == 0) return Enumerable.Empty<Live<TModel>>();

        var imported = new List<Live<TModel>>();
        isBatch |= tasks.Length >= minimum_items_considered_batch_import;

        await Task.WhenAll(tasks.Select(async task =>
        {
            try
            {
                var model = await Import(task, isBatch, default).ConfigureAwait(false);

                lock (imported)
                {
                    if (model != null)
                        imported.Add(model);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Could not import ({task})", LoggingTarget.Database);
            }
        })).ConfigureAwait(false);

        if (imported.Count > 0 && PresentImport != null)
        {
            PresentImport?.Invoke(imported);
        }

        return imported;
    }

    public virtual Task<Live<TModel>?> ImportAsUpdate(ImportTask task, TModel original) => throw new NotImplementedException();

    public async Task<Live<TModel>?> Import(ImportTask task, bool isBatch = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Live<TModel>? import;
        using (ArchiveReader reader = task.GetReader())
            import = await importFromArchive(reader, isBatch, cancellationToken).ConfigureAwait(false);

        // We may or may not want to delete the file depending on where it is stored.
        //   e.g. reconstructing/repairing database with items from default storage.
        // Also, not always a single file, i.e. for FilesystemReader
        // TODO: Add a check to prevent files from storage to be deleted.
        try
        {
            if (import != null && ShouldDeleteArchive(task.Path))
                task.DeleteFile();
        }
        catch (Exception e)
        {
            Logger.Error(e, $@"Could not delete original file after import ({task})");
        }

        return import;
    }

    private async Task<Live<TModel>?> importFromArchive(ArchiveReader archive, bool isBatch = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TModel? model = null;

        try
        {
            model = CreateModel(archive);

            if (model == null)
                return null;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            LogForModel(model, $@"Model creation of {archive.Name} failed.", e);
            return null;
        }

        var scheduledImport = Task.Factory.StartNew(() => ImportModel(model, archive, isBatch, cancellationToken),
            cancellationToken,
            TaskCreationOptions.HideScheduler,
            isBatch ? import_scheduler_batch : import_scheduler);

        return await scheduledImport.ConfigureAwait(false);
    }

    public virtual Live<TModel>? ImportModel(TModel item, ArchiveReader? archive = null, bool isBatch = false, CancellationToken cancellationToken = default)
        => Realm.Run(realm =>
        {
            TModel? existing;

            if (isBatch && archive != null)
            {
                // this is a fast bail condition to improve large import performance.
                item.Hash = computeHashFast(archive);

                existing = CheckForExisting(item, realm);

                if (existing != null)
                {
                    // bare minimum comparisons
                    //
                    // note that this should really be checking filesizes on disk (of existing files) for some degree of sanity.
                    // or alternatively doing a faster hash check. either of these require database changes and reprocessing of existing files.
                    if (CanSkipImport(existing, item) &&
                        getFilenames(existing.Files).SequenceEqual(getShortenedFilenames(archive).Select(p => p.shortened).OrderBy(f => f)) &&
                        checkAllFilesExist(existing))
                    {
                        LogForModel(item, $@"Found existing (optimised) {HumanisedModelName} for {item} (ID {existing.ID}) - skipping import.");

                        using (var transaction = realm.BeginWrite())
                        {
                            UndeleteForReuse(existing);
                            transaction.Commit();
                        }

                        return existing.ToLive(Realm);
                    }

                    LogForModel(item, @"Found existing (optimised) but failed pre-check.");
                }
            }

            try
            {
                // Log output here will be missing a valid hash in non-batch imports.
                LogForModel(item, $@"Beginning import from {archive?.Name ?? "unknown"}...");

                var files = new List<RealmNamedFileUsage>();

                if (archive != null)
                {
                    // Import files to the disk store.
                    // We intentionally delay adding to realm to avoid blocking on a write during disk operations.
                    foreach (var filenames in getShortenedFilenames(archive))
                    {
                        using (Stream s = archive.GetStream(filenames.original)!)
                            files.Add(new RealmNamedFileUsage(Files.Add(s, realm, false), filenames.shortened));
                    }
                }

                using (var transaction = realm.BeginWrite())
                {
                    // Add all files to realm in one go.
                    // This is done ahead of the main transaction to ensure we can correctly cleanup the files, even if the import fails.
                    foreach (var file in files)
                    {
                        if (!file.File.IsManaged)
                            realm.Add(file.File, true);
                    }

                    transaction.Commit();
                }

                item.Files.AddRange(files);
                item.Hash = ComputeHash(item);

                // TODO: do we want to make the transaction this local? not 100% sure, will need further investigation
                using (var transaction = realm.BeginWrite())
                {
                    // TODO: we may want to run this outside of the transaction.
                    Populate(item, archive, realm, cancellationToken);

                    // Populate() may have adjusted file content, so regardless of whether a fast check was done earlier, let's
                    // check for existing items a second time.
                    //
                    // If this is ever a performance issue, the fast-check hash can be compared and trigger a skip of this second check if it still matches.
                    // I don't think it is a huge deal doing a second indexed check, though.
                    existing = CheckForExisting(item, realm);

                    if (existing != null)
                    {
                        if (CanReuseExisting(existing, item))
                        {
                            LogForModel(item, $@"Found existing {HumanisedModelName} for {item} (ID {existing.ID}) - skipping import.");

                            UndeleteForReuse(existing);
                            transaction.Commit();

                            return existing.ToLive(Realm);
                        }

                        LogForModel(item, @"Found existing but failed re-use check.");
                        existing.DeletePending = true;
                    }

                    PreImport(item, realm);

                    // import to store
                    realm.Add(item);

                    PostImport(item, realm, isBatch);

                    transaction.Commit();
                }

                LogForModel(item, @"Import successfully completed!");
            }
            catch (Exception e)
            {
                if (!(e is TaskCanceledException))
                    LogForModel(item, @"Database import or population failed and has been rolled back", e);

                throw;
            }

            return (Live<TModel>?) item.ToLive(Realm);
        });
    
    /// <summary>
    /// Any file extensions which should be included in hash creation.
    /// Generally should include all file types which determine the file's uniqueness.
    /// Large files should be avoided if possible.
    /// </summary>
    /// <remarks>
    /// This is only used by the default hash implementation If <see cref="ComputeHash"/> is overriden, it will not be used.
    /// </remarks>
    protected abstract string[] HashableFileTypes { get; }
    
    internal static void LogForModel(TModel? model, string message, Exception? e = null)
    {
        string trimmedHash;
        if (model == null || !model.IsValid || string.IsNullOrEmpty(model.Hash))
            trimmedHash = "?????";
        else
            trimmedHash = model.Hash.Substring(0, 5);

        var prefix = $"[{trimmedHash}]";

        if (e != null)
            Logger.Error(e, $"{prefix} {message}", LoggingTarget.Database);
        else
            Logger.Log($"{prefix} {message}", LoggingTarget.Database);
    }
    
    /// <summary>
    /// Create a SHA-2 hash from the provided archive based on file content of all files matching <see cref="HashableFileTypes"/>.
    /// </summary>
    /// <remarks>
    /// In the case of no matching files, a hash will be generated from the passed archive's <see cref="ArchiveReader.Name"/>.
    /// </remarks>
    public string ComputeHash(TModel item)
    {
        // for now, concatenate all hashable files in the set to create a unique hash.
        var hashable = new MemoryStream();

        foreach (var file in item.Files.Where(f => HashableFileTypes.Any(ext => f.Filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).OrderBy(f => f.Filename))
        {
            using (Stream s = Files.Store.GetStream(file.File.GetStoragePath()))
                s.CopyTo(hashable);
        }

        if (hashable.Length > 0)
            return hashable.ComputeSHA2Hash();

        return item.Hash;
    }
    
    private string computeHashFast(ArchiveReader reader)
    {
        var hashable = new MemoryStream();

        foreach (var file in reader.Filenames.Where(f => HashableFileTypes.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).OrderBy(f => f))
        {
            using (Stream? s = reader.GetStream(file))
                s?.CopyTo(hashable);
        }

        if (hashable.Length > 0)
            return hashable.ComputeSHA2Hash();

        return reader.Name.ComputeSHA2Hash();
    }

    private IEnumerable<(string original, string shortened)> getShortenedFilenames(ArchiveReader reader)
    {
        var prefix = reader.Filenames.GetCommonPrefix();
        if (!(prefix.EndsWith('/') || prefix.EndsWith('\\')))
            prefix = string.Empty;

        foreach (var file in reader.Filenames)
            yield return (file, file.Substring(prefix.Length).ToStandardisedPath());
    }
    
    /// <summary>
    /// Create a barebones model from the provided archive.
    /// Actual expensive population should be done in <see cref="Populate"/>; this should just prepare for duplicate checking.
    /// </summary>
    /// <param name="reader">The archive to create the model for.</param>
    /// <returns>A model populated with minimal information. Returning a null will abort importing silently.</returns>
    protected abstract TModel? CreateModel(ArchiveReader reader);

    /// <summary>
    /// Populate the provided model completely from the given archive.
    /// After this method, the model should be in a state ready to commit to a store.
    /// </summary>
    /// <param name="model">The model to populate.</param>
    /// <param name="archive">The archive to use as a reference for population. May be null.</param>
    /// <param name="realm">The current realm context.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    protected abstract void Populate(TModel model, ArchiveReader? archive, Realm realm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform any final actions before the import to database executes.
    /// </summary>
    /// <param name="model">The model prepared for import.</param>
    /// <param name="realm">The current realm context.</param>
    protected virtual void PreImport(TModel model, Realm realm)
    {
    }

    /// <summary>
    /// Perform any final actions before the import has been committed to the database.
    /// </summary>
    /// <param name="model">The model prepared for import.</param>
    /// <param name="realm">The current realm context.</param>
    /// <param name="isBatch">Whether this import is part of a larger batch.</param>
    protected virtual void PostImport(TModel model, Realm realm, bool isBatch)
    {
    }

    /// <summary>
    /// Check whether an existing model already exists for a new import item.
    /// </summary>
    /// <param name="model">The new model proposed for import.</param>
    /// <param name="realm">The current realm context.</param>
    /// <returns>An existing model which matches the criteria to skip importing, else null.</returns>
    protected TModel? CheckForExisting(TModel model, Realm realm)
        => string.IsNullOrEmpty(model.Hash) ? null : realm.All<TModel>().FirstOrDefault(b => b.Hash == model.Hash);

    /// <summary>
    /// Whether import can be skipped after finding an existing import early in the process.
    /// Only valid when <see cref="ComputeHash"/> is not overriden.
    /// </summary>
    /// <param name="existing">The existing model.</param>
    /// <param name="import">The newly imported model.</param>
    /// <returns>Whether to skip this import completely.</returns>
    protected virtual bool CanSkipImport(TModel existing, TModel import) => true;

    protected virtual bool CanReuseExisting(TModel existing, TModel import)
        // for better or worse, we copy and import files of a new import before checking whether
        // it is a duplicate. so to check if anything has changed, we can just compare all File IDs.
        => getIDs(existing.Files).SequenceEqual(getIDs(import.Files)) && getFilenames(existing.Files).SequenceEqual(getFilenames(import.Files));

    private bool checkAllFilesExist(TModel model)
        => model.Files.All(f => Files.Storage.Exists(f.File.GetStoragePath()));

    /// <summary>
    /// Called when an existing model is in a soft deleted state but being recovered.
    /// </summary>
    /// <param name="existing">The existing model.</param>
    protected virtual void UndeleteForReuse(TModel existing)
    {
        if (!existing.DeletePending)
            return;

        LogForModel(existing, $@"Existing {HumanisedModelName}'s deletion flag has been removed to allow for reuse.");
        existing.DeletePending = false;
    }

    /// <summary>
    /// Whether this specified path should be removed after successful import.
    /// </summary>
    /// <param name="path">The path for consideration. May be a file or a directory.</param>
    /// <returns>Whether to perform deletion.</returns>
    protected virtual bool ShouldDeleteArchive(string path) => false;

    private IEnumerable<string> getIDs(IEnumerable<INamedFile> files)
    {
        foreach (var f in files.OrderBy(f => f.Filename))
            yield return f.File.Hash;
    }

    private IEnumerable<string> getFilenames(IEnumerable<INamedFile> files)
    {
        foreach (var f in files.OrderBy(f => f.Filename))
            yield return f.Filename;
    }

    public virtual string HumanisedModelName => $"{typeof(TModel).Name.Replace(@"Info", "").ToLowerInvariant()}";
}
