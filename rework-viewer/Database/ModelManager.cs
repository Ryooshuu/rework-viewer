using System.Diagnostics;
using NuGet.Packaging;
using osu.Framework.Platform;
using Realms;
using rework_viewer.Extensions;
using rework_viewer.Models;

namespace rework_viewer.Database;

public class ModelManager<TModel> : IModelManager<TModel>, IModelFileManager<TModel, RealmNamedFileUsage>
    where TModel : RealmObject, IHasRealmFiles, IHasGuidPrimaryKey, ISoftDelete
{
    protected RealmAccess Realm { get; }

    private readonly RealmFileStore realmFileStore;

    public ModelManager(Storage storage, RealmAccess realm)
    {
        realmFileStore = new RealmFileStore(realm, storage);
        Realm = realm;
    }

    public void DeleteFile(TModel item, RealmNamedFileUsage file)
        => performFileOperation(item, managed => DeleteFile(managed, managed.Files.First(f => f.Filename == file.Filename), managed.Realm));

    public void ReplaceFile(TModel item, RealmNamedFileUsage file, Stream contents)
        => performFileOperation(item, managed => ReplaceFile(file, contents, managed.Realm));

    public void AddFile(TModel item, Stream contents, string filename)
        => performFileOperation(item, managed => AddFile(managed, contents, filename, managed.Realm));

    private void performFileOperation(TModel item, Action<TModel> operation)
    {
        // While we are detaching so often, this seems like the easiest way to keep things in sync.
        // This method should be removed as soon as all the surrounding pieces support non-detached operations.
        if (!item.IsManaged)
        {
            var r = Realm.Realm;
            r.Write(() =>
            {
                var managed = r.Find<TModel>(item.ID);
                Debug.Assert(managed != null);
                operation(managed);

                item.Files.Clear();
                item.Files.AddRange(managed.Files.Detach());
            });
        }
    }

    /// <summary>
    /// Delete a file from within an ongoing realm transaction.
    /// </summary>
    public void DeleteFile(TModel item, RealmNamedFileUsage file, Realm realm)
    {
        item.Files.Remove(file);
    }

    /// <summary>
    /// Replace a file from within an ongoing realm transaction.
    /// </summary>
    public void ReplaceFile(RealmNamedFileUsage file, Stream contents, Realm realm)
    {
        file.File = realmFileStore.Add(contents, realm);
    }

    /// <summary>
    /// Add a file from within an ongoing realm transaction. If the file already exists, it is overwritten.
    /// </summary>
    public void AddFile(TModel item, Stream contents, string filename, Realm realm)
    {
        var existing = item.GetFile(filename);

        if (existing != null)
        {
            ReplaceFile(existing, contents, realm);
            return;
        }

        var file = realmFileStore.Add(contents, realm);
        var namedUsage = new RealmNamedFileUsage(file, filename);

        item.Files.Add(namedUsage);
    }

    /// <summary>
    /// Delete multiple items.
    /// This will post notifications tracking progress.
    /// </summary>
    public void Delete(List<TModel> items)
    {
        if (items.Count == 0) return;

        // TODO: Notification

        foreach (var item in items)
        {
            Delete(item);
        }
    }

    /// <summary>
    /// Restore multiple items that were previously deleted.
    /// This will post notifications tracking progress.
    /// </summary>
    public void Undelete(List<TModel> items)
    {
        if (!items.Any()) return;

        // TODO: Notification

        foreach (var item in items)
        {
            Undelete(item);
        }
    }

    public bool Delete(TModel item)
    {
        // Importantly, begin the realm write *before* re-fetching, else the update realm may not be in a consistent state
        // (ie. if an async import finished very recently).
        return Realm.Write(realm =>
        {
            if (!item.IsManaged)
                item = realm.Find<TModel>(item.ID);

            if (item?.DeletePending != false)
                return false;

            item.DeletePending = true;
            return true;
        });
    }

    public void Undelete(TModel item)
    {
        // Importantly, begin the realm write *before* re-fetching, else the update realm may not be in a consistent state
        // (ie. if an async import finished very recently).
        Realm.Write(realm =>
        {
            if (!item.IsManaged)
                item = realm.Find<TModel>(item.ID);

            if (item?.DeletePending != true)
                return;

            item.DeletePending = false;
        });
    }

    public virtual bool IsAvailableLocally(TModel model) => true;

    public virtual string HumanisedModelName => $"{typeof(TModel).Name.Replace(@"Info", "").ToLowerInvariant()}";
}
