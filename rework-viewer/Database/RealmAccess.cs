
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Realms;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace rework_viewer.Database;

public class RealmAccess : IDisposable
{
    private readonly Storage storage;

    /// <summary>
    /// The filename of this realm instance.
    /// </summary>
    public readonly string Filename;

    private readonly SynchronizationContext? threadSyncContext;

    /// <summary>
    /// Version history:
    /// 1  2023-04-08  First tracked
    /// </summary>
    private const int schema_version = 1;

    /// <summary>
    /// Holds a map of functions registered via <see cref="RegisterCustomSubscription"/> and <see cref="RegisterForNotifications{T}"/> and a coinciding action which when triggered,
    /// will unregister the subscription from realm.
    ///
    /// Put another way, the key is an action which registers the subscription with realm. The returned <see cref="IDisposable"/> from the action is stored as the value and only
    /// used internally.
    ///
    /// Entries in this dictionary are only removed when a consumer signals that the subscription should be permanently ceased (via their own <see cref="IDisposable"/>).
    /// </summary>
    private readonly Dictionary<Func<Realm, IDisposable?>, IDisposable?> customSubscriptionsResetMap = new();

    private Realm? updateRealm;

    public Realm Realm => ensureRealm();

    private const string realm_extension = @".realm";

    private Realm ensureRealm()
    {
        if (updateRealm == null)
        {
            updateRealm = getRealmInstance();

            Logger.Log($@"Opened realm ""{updateRealm.Config.DatabasePath}"" at version {updateRealm.Config.SchemaVersion}");

            foreach (var action in customSubscriptionsResetMap.Keys.ToArray())
                registerSubscription(action);
        }

        Debug.Assert(updateRealm != null);
        return updateRealm;
    }

    internal static bool CurrentThreadSubscriptionsAllowed => current_thread_subscriptions_allowed.Value;

    private static readonly ThreadLocal<bool> current_thread_subscriptions_allowed = new();

    /// <summary>
    /// Constructs a new instance.
    /// </summary>
    /// <param name="storage">The game storage which will be used to create the realm backing file.</param>
    /// <param name="filename">The filename to use for the realm backing file. A ".realm" extension will be added automatically if not specified.</param>
    public RealmAccess(Storage storage, string filename)
    {
        this.storage = storage;

        threadSyncContext = SynchronizationContext.Current;

        Filename = filename;

        if (!Filename.EndsWith(realm_extension, StringComparison.Ordinal))
            Filename += realm_extension;

#if DEBUG
        if (!DebugUtils.IsNUnitRunning)
            applyFilenameSchemaSuffix(ref Filename);
#endif

        var newerVersionFilename = $"{Filename.Replace(realm_extension, string.Empty)}_newer_version{realm_extension}";

        if (storage.Exists(newerVersionFilename))
        {
            Logger.Log(@"A newer realm database has been found, attempting recovery...", LoggingTarget.Database);
            attemptRecoverFromFile(newerVersionFilename);
        }

        try
        {
            // This method triggers the first `getRealmInstance` call, which will implicitly run realm migrations and bring the schema up-to-date.
            cleanupPendingDeletions();
        }
        catch (Exception e)
        {
            // See https://github.com/realm/realm-core/blob/master/src%2Frealm%2Fobject-store%2Fobject_store.cpp#L1016-L1022
            // This is the best way we can detect a schema version downgrade.
            if (e.Message.StartsWith(@"Provided schema version", StringComparison.Ordinal))
            {
                Logger.Error(e, "Your local database is too new to work with this version. Please close this application and install the latest release to recover your data.");

                if (!storage.Exists(newerVersionFilename))
                    createBackup(newerVersionFilename);

                storage.Delete(Filename);
            }
            else
            {
                Logger.Error(e, "Realm startup failed with unrecoverable error; starting with a fresh database. A backup of your database has been made.");
                createBackup($"{Filename.Replace(realm_extension, string.Empty)}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_corrupt{realm_extension}");
                storage.Delete(filename);
            }

            cleanupPendingDeletions();
        }
    }

    /// <summary>
    /// Some developers may be annoyed if a newer version migration (ie. caused by testing a pull request)
    /// cause their test database to be unusable with previous versions.
    /// To get around this, store development databases against their realm version.
    /// Note that this means changes made on newer realm versions will disappear.
    /// </summary>
    private void applyFilenameSchemaSuffix(ref string filename)
    {
        var originalFilename = filename;

        filename = getVersionedFilename(schema_version);

        // First check if the current realm version already exists...
        if (storage.Exists(filename))
            return;

        // Check for a previous version we can use as a base database to migrate from...
        for (var i = schema_version - 1; i >= 0; i--)
        {
            var previousFilename = getVersionedFilename(i);

            if (storage.Exists(previousFilename))
            {
                copyPreviousVersion(previousFilename, filename);
                return;
            }
        }

        // Finally, check for a non-versioned file exists (aka before this method was added)...
        if (storage.Exists(originalFilename))
            copyPreviousVersion(originalFilename, filename);

        void copyPreviousVersion(string previousFilename, string newFilename)
        {
            using var previous = storage.GetStream(previousFilename);
            using var current = storage.CreateFileSafely(newFilename);

            Logger.Log($@"Copying previous realm database {previousFilename} to {newFilename} for migration to schema version {schema_version}");
            previous.CopyTo(current);
        }

        string getVersionedFilename(int version) => originalFilename.Replace(realm_extension, $"_{version}{realm_extension}");
    }

    private void attemptRecoverFromFile(string recoveryFilename)
    {
        Logger.Log($@"Performing recovery from {recoveryFilename}", LoggingTarget.Database);

        // First check the user hasn't started to use the database that is in place..
        try
        {
            // using (var realm = Realm.GetInstance(getConfiguration()))
            // {
            //     if (realm.All<BeatmapInfo>().Any())
            //     {
            //         Logger.Log(@"Recovery aborted as the existing database has scores set already.", LoggingTarget.Database);
            //         Logger.Log($@"To perform recovery, delete {PaGameBase.CLIENT_DATABASE_FILENAME} while the application is not running.", LoggingTarget.Database);
            //         return;
            //     }
            // }
        }
        catch
        {
            // Even if reading the in place database fails, still attempt to recover.
        }

        try
        {
            using (Realm.GetInstance(getConfiguration(recoveryFilename)))
            {
                // Don't need to do anything, just check that opening the realm works correctly.
            }
        }
        catch
        {
            Logger.Log(@"Recovery aborted as the newer version could not be loaded by this version.", LoggingTarget.Database);
            return;
        }

        createBackup($"{Filename.Replace(realm_extension, string.Empty)}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_newer_version_before_recovery{realm_extension}");

        storage.Delete(Filename);

        using (var inputStream = storage.GetStream(recoveryFilename))
        using (var outputStream = storage.CreateFileSafely(Filename))
            inputStream.CopyTo(outputStream);

        storage.Delete(recoveryFilename);
        Logger.Log(@"Recovery complete!", LoggingTarget.Database);
    }

    private void cleanupPendingDeletions()
    {
        using (var realm = getRealmInstance())
        using (var transaction = realm.BeginWrite())
        {
            // var pendingDeleteBeatmaps = realm.All<RealmBeatmap>().Where(s => s.DeletePending);
            //
            // foreach (var s in pendingDeleteBeatmaps)
            //     realm.Remove(s);

            transaction.Commit();
        }

        // clean up files after dropping any pending deletions.
        // in the future we may want to only do this when the game is idle, rather than on every startup.
        new RealmFileStore(this, storage).Cleanup();
    }

    /// <summary>
    /// Compact this realm.
    /// </summary>
    public bool Compact()
    {
        return Realm.Compact(getConfiguration());
    }

    /// <summary>
    /// Run work on realm with a return value.
    /// </summary>
    /// <param name="action">The work to run.</param>
    /// <typeparam name="T">The return type.</typeparam>
    public T Run<T>(Func<Realm, T> action)
    {
        using (var realm = getRealmInstance())
            return action(realm);
    }

    /// <summary>
    /// Run work on realm.
    /// </summary>
    /// <param name="action">The work to run.</param>
    public void Run(Action<Realm> action)
    {
        using (var realm = getRealmInstance())
            action(realm);
    }

    /// <summary>
    /// Write changes to realm.
    /// </summary>
    /// <param name="action">The work to run.</param>
    public T Write<T>(Func<Realm, T> action)
    {
        using (var realm = getRealmInstance())
            return realm.Write(() => action(realm));
    }


    /// <summary>
    /// Write changes to realm.
    /// </summary>
    /// <param name="action">The work to run.</param>
    public void Write(Action<Realm> action)
    {
        using (var realm = getRealmInstance())
            realm.Write(() => action(realm));
    }

    private readonly CountdownEvent pendingAsyncWrites = new(0);

    /// <summary>
    /// Write changes to realm asynchronously, guaranteeing order of execution.
    /// </summary>
    /// <param name="action">The work to run.</param>
    public Task WriteAsync(Action<Realm> action)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(RealmAccess));

        // CountdownEvent will fail if already at zero.
        if (!pendingAsyncWrites.TryAddCount())
            pendingAsyncWrites.Reset(1);

        var writeTask = Task.Run(async () =>
        {
            // Not attempting to use Realm.GetInstanceAsync as there's seemingly no benefit to us (for now) and it adds complexity due to locking
            // concerns in getRealmInstance(). On a quick check, it looks to be more suited to cases where realm is connecting to an online sync
            // server, which we don't use. May want to report upstream or revisit in the future.
            using (var realm = getRealmInstance())
                // ReSharper disable once AccessToDisposedClosure (WriteAsync should be marked as [InstantHandle]).
                await realm.WriteAsync(() => action(realm)).ConfigureAwait(false);

            pendingAsyncWrites.Signal();
        });

        return writeTask;
    }

    /// <summary>
    /// Subscribe to a realm collection and begin watching for asynchronous changes.
    /// </summary>
    /// <remarks>
    /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
    ///
    /// In addition to the documented realm behaviour, we have the additional requirement of handling subscriptions over potential realm instance recycle.
    /// When this happens, callback events will be automatically fired:
    /// - On recycle start, a callback with an empty collection and <c>null</c> <see cref="ChangeSet"/> will be invoked.
    /// - On recycle end, a standard initial realm callback will arrive, with <c>null</c> <see cref="ChangeSet"/> and an up-to-date collection.
    /// </remarks>
    /// <param name="query">The <see cref="IQueryable{T}"/> to observe for changes.</param>
    /// <typeparam name="T">Type of the elements in the list.</typeparam>
    /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
    /// <returns>
    /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
    /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
    /// </returns>
    /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
    public IDisposable RegisterForNotifications<T>(Func<Realm, IQueryable<T>> query, NotificationCallbackDelegate<T> callback)
        where T : RealmObjectBase
    {
        Func<Realm, IDisposable?> action = realm => query(realm).QueryAsyncWithNotifications(callback);

        return RegisterCustomSubscription(action);
    }

    /// <summary>
    /// Run work on realm that will be run every time the update thread realm instance gets recycled.
    /// </summary>
    /// <param name="action">The work to run. Return value should be an <see cref="IDisposable"/> from QueryAsyncWithNotifications, or an <see cref="InvokeOnDisposal"/> to clean up any bindings.</param>
    /// <returns>An <see cref="IDisposable"/> which should be disposed to unsubscribe any inner subscription.</returns>
    public IDisposable RegisterCustomSubscription(Func<Realm, IDisposable?> action)
    {
        if (threadSyncContext == null)
            throw new InvalidOperationException("Attempted to register a realm subscription before update thread registration.");

        threadSyncContext.Post(_ => registerSubscription(action), null);

        // This token is returned to the consumer.
        // When disposed, it will cause the registration to be permanently ceased (unsubscribed with realm and unregistered by this class).
        return new InvokeOnDisposal(() =>
        {
            threadSyncContext.Post(_ => unsubscribe(), null);

            void unsubscribe()
            {
                if (customSubscriptionsResetMap.TryGetValue(action, out var unsubscriptionAction))
                {
                    unsubscriptionAction?.Dispose();
                    customSubscriptionsResetMap.Remove(action);
                }
            }
        });
    }

    private void registerSubscription(Func<Realm, IDisposable?> action)
    {
        // Retrieve realm instance outside of flag update to ensure that the instance is retrieved,
        // as attempting to access it inside the subscription if it's not constructed would lead to
        // cyclic invocations of the subscription callback.
        var realm = Realm;

        Debug.Assert(!customSubscriptionsResetMap.TryGetValue(action, out var found) || found == null);

        current_thread_subscriptions_allowed.Value = true;
        customSubscriptionsResetMap[action] = action(realm);
        current_thread_subscriptions_allowed.Value = false;
    }

    private Realm getRealmInstance()
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(RealmAccess));

        return Realm.GetInstance(getConfiguration());
    }

    private RealmConfiguration getConfiguration(string? filename = null)
    {
        // This is currently the only usage of temporary files at the osu! side.
        // If we use the temporary folder in more situations in the future, this should be moved to a higher level (helper method or OsuGameBase).
        string tempPathLocation = Path.Combine(Path.GetTempPath(), @"rework-viewer");
        if (!Directory.Exists(tempPathLocation))
            Directory.CreateDirectory(tempPathLocation);

        return new RealmConfiguration(storage.GetFullPath(filename ?? Filename, true))
        {
            SchemaVersion = schema_version,
            MigrationCallback = onMigration,
            FallbackPipePath = tempPathLocation
        };
    }

    private void onMigration(Migration migration, ulong lastSchemaVersion)
    {
        for (var i = lastSchemaVersion + 1; i <= schema_version; i++)
            applyMigrationsForVersion(migration, i);
    }

    private void applyMigrationsForVersion(Migration migration, ulong targetVersion)
    {
        switch (targetVersion)
        {
        }
    }

    private void createBackup(string backupFilename)
    {
        Logger.Log($"Create full realm database backup at {backupFilename}", LoggingTarget.Database);

        var attempts = 10;

        while (attempts-- > 0)
        {
            try
            {
                using (var source = storage.GetStream(Filename, mode: FileMode.Open))
                {
                    // source may not exist.
                    if (source == null)
                        return;

                    using (var destination = storage.GetStream(backupFilename, FileAccess.Write, FileMode.CreateNew))
                        source.CopyTo(destination);
                }

                return;
            }
            catch (IOException)
            {
                // file may be locked during use.
                Thread.Sleep(500);
            }
        }
    }
    
    private bool isDisposed;

    public void Dispose()
    {
        if (!pendingAsyncWrites.Wait(10000))
            Logger.Log("Realm took too long waiting on pending async writes", level: LogLevel.Error);

        updateRealm?.Dispose();

        if (!isDisposed)
        {
            isDisposed = true;
        }
    }
}
