using Realms;

namespace rework_viewer.Database;

/// <summary>
/// Provides a method of working with realm objects over longer application lifetimes.
/// </summary>
/// <typeparam name="T">The underlying object type.</typeparam>
public class RealmLive<T> : Live<T>
    where T : RealmObject, IHasGuidPrimaryKey
{
    public override bool IsManaged => data.IsManaged;

    /// <summary>
    /// The original live data used to create this instance.
    /// </summary>
    private T data;

    private readonly RealmAccess realm;

    /// <summary>
    /// Construct a new instance of live realm data.
    /// </summary>
    /// <param name="data">The realm data.</param>
    /// <param name="realm">The realm factory the data was sourced from. May be null for an unmanaged object.</param>
    public RealmLive(T data, RealmAccess realm)
        : base(data.ID)
    {
        this.data = data;
        this.realm = realm;
    }

    /// <summary>
    /// Perform a read operation on this live object.
    /// </summary>
    /// <param name="perform">The action to perform.</param>
    public override void PerformRead(Action<T> perform)
    {
        if (!IsManaged)
        {
            perform(data);
            return;
        }

        realm.Run(r =>
        {
            perform(retrieveFromId(r));
        });
    }

    /// <summary>
    /// Perform a read operation on this live object.
    /// </summary>
    /// <param name="perform">The action to perform.</param>
    public override TReturn PerformRead<TReturn>(Func<T, TReturn> perform)
    {
        if (!IsManaged)
            return perform(data);

        return realm.Run(r =>
        {
            var returnData = perform(retrieveFromId(r));

            if (returnData is RealmObjectBase realmObject && realmObject.IsManaged)
                throw new InvalidOperationException($@"Managed realm objects should not exit the scope of {nameof(PerformRead)}.");

            return returnData;
        });
    }

    /// <summary>
    /// Perform a write operation on this live object.
    /// </summary>
    /// <param name="perform">The action to perform.</param>
    public override void PerformWrite(Action<T> perform)
    {
        if (!IsManaged)
            throw new InvalidOperationException(@"Can't perform writes on a non-managed underlying value.");

        PerformRead(t =>
        {
            using (var transaction = t.Realm.BeginWrite())
            {
                perform(t);
                transaction.Commit();
            }
        });
    }

    public override T Value
    {
        get
        {
            if (!IsManaged)
                return data;

            ensureDataIsFromUpdateThread();
            return data;
        }
    }

    private void ensureDataIsFromUpdateThread()
    {
        data = retrieveFromId(realm.Realm);
    }

    private T retrieveFromId(Realm realm)
    {
        var found = realm.Find<T>(ID);

        if (found == null)
        {
            // It may be that we access this from the update thread before a refresh has taken place.
            // To ensure that behaviour matches what we'd expect (the object *is* available), force
            // a refresh to bring in any off-thread changes immediately.
            realm.Refresh();
            found = realm.Find<T>(ID);
        }

        return found;
    }
}
