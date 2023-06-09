﻿using System.Runtime.Serialization;
using AutoMapper;
using AutoMapper.Internal;
using Realms;
using rework_viewer.Models;

namespace rework_viewer.Database;

public static class RealmObjectExtension
{
    private static readonly IMapper mapper = new MapperConfiguration(applyCommonConfiguration).CreateMapper();
    
    private static void applyCommonConfiguration(IMapperConfigurationExpression c)
    {
        c.ShouldMapField = _ => false;

        // This is specifically to avoid mapping explicit interface implementations.
        // If we want to limit this further, we can avoid mapping properties with no setter that are not IList<>.
        // Takes a bit of effort to determine whether this is the case though, see https://stackoverflow.com/questions/951536/how-do-i-tell-whether-a-type-implements-ilist
        c.ShouldMapProperty = pi => pi.GetMethod?.IsPublic == true;

        c.Internal().ForAllMaps((_, expression) =>
        {
            expression.ForAllMembers(m =>
            {
                if (m.DestinationMember.Has<IgnoredAttribute>() || m.DestinationMember.Has<BacklinkAttribute>() || m.DestinationMember.Has<IgnoreDataMemberAttribute>())
                    m.Ignore();
            });
        });

        c.CreateMap<RealmFile, RealmFile>();
        c.CreateMap<RealmNamedFileUsage, RealmNamedFileUsage>();
    }
    
    /// <summary>
    /// Create a detached copy of each item in the collection.
    /// </summary>
    /// <remarks>
    /// Items which are already detached (ie. not managed by realm) will not be modified.
    /// </remarks>
    /// <param name="items">A list of managed <see cref="RealmObject"/>s to detach</param>
    /// <typeparam name="T">The type of object.</typeparam>
    /// <returns>A list containing non-managed copies of provided items.</returns>
    public static List<T> Detach<T>(this IEnumerable<T> items)
        where T : RealmObjectBase
    {
        var list = new List<T>();

        foreach (var obj in items)
            list.Add(obj.Detach());

        return list;
    }

    /// <summary>
    /// Create a detached copy of the item.
    /// </summary>
    /// <remarks>
    /// If the item if already detached (ie. not managed by realm) it will not be detached again and the original instance will be returned. This allows this method to be potentially called at multiple levels while only incurring the clone overhead once.
    /// </remarks>
    /// <param name="item">The managed <see cref="RealmObject"/> to detach.</param>
    /// <typeparam name="T">The type of object.</typeparam>
    /// <returns>A non-managed copy of provided item. Will return the provided item if already detached.</returns>
    public static T Detach<T>(this T item)
        where T : RealmObjectBase
    {
        if (!item.IsManaged)
            return item;

        return mapper.Map<T>(item);
    }

    public static Live<T> ToLive<T>(this T realmObject, RealmAccess realm)
        where T : RealmObject, IHasGuidPrimaryKey
    {
        return new RealmLive<T>(realmObject, realm);
    }
    
    /// <summary>
    /// Register a callback to be invoked each time this <see cref="T:Realms.IRealmCollection`1" /> changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
    /// </para>
    /// <para>
    /// The first callback will be invoked with the initial <see cref="T:Realms.IRealmCollection`1" /> after the asynchronous query completes,
    /// and then called again after each write transaction which changes either any of the objects in the collection, or
    /// which objects are in the collection. The <c>changes</c> parameter will
    /// be <c>null</c> the first time the callback is invoked with the initial results. For each call after that,
    /// it will contain information about which rows in the results were added, removed or modified.
    /// </para>
    /// <para>
    /// If a write transaction did not modify any objects in this <see cref="T:Realms.IRealmCollection`1" />, the callback is not invoked at all.
    /// If an error occurs the callback will be invoked with <c>null</c> for the <c>sender</c> parameter and a non-<c>null</c> <c>error</c>.
    /// Currently the only errors that can occur are when opening the <see cref="T:Realms.Realm" /> on the background worker thread.
    /// </para>
    /// <para>
    /// At the time when the block is called, the <see cref="T:Realms.IRealmCollection`1" /> object will be fully evaluated
    /// and up-to-date, and as long as you do not perform a write transaction on the same thread
    /// or explicitly call <see cref="M:Realms.Realm.Refresh" />, accessing it will never perform blocking work.
    /// </para>
    /// <para>
    /// Notifications are delivered via the standard event loop, and so can't be delivered while the event loop is blocked by other activity.
    /// When notifications can't be delivered instantly, multiple notifications may be coalesced into a single notification.
    /// This can include the notification with the initial collection.
    /// </para>
    /// </remarks>
    /// <param name="collection">The <see cref="IRealmCollection{T}"/> to observe for changes.</param>
    /// <param name="callback">The callback to be invoked with the updated <see cref="T:Realms.IRealmCollection`1" />.</param>
    /// <returns>
    /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
    /// To stop receiving notifications, call <see cref="M:System.IDisposable.Dispose" />.
    ///
    /// May be null in the case the provided collection is not managed.
    /// </returns>
    /// <seealso cref="M:Realms.CollectionExtensions.SubscribeForNotifications``1(System.Collections.Generic.IList{``0},Realms.NotificationCallbackDelegate{``0})" />
    /// <seealso cref="M:Realms.CollectionExtensions.SubscribeForNotifications``1(System.Linq.IQueryable{``0},Realms.NotificationCallbackDelegate{``0})" />
    public static IDisposable? QueryAsyncWithNotifications<T>(this IRealmCollection<T> collection, NotificationCallbackDelegate<T> callback)
        where T : RealmObjectBase
    {
        if (!RealmAccess.CurrentThreadSubscriptionsAllowed)
            throw new InvalidOperationException($"Make sure to call {nameof(RealmAccess)}.{nameof(RealmAccess.RegisterForNotifications)}");

        return collection.SubscribeForNotifications(callback);
    }
    
    /// <summary>
    /// A convenience method that casts <see cref="IQueryable{T}"/> to <see cref="IRealmCollection{T}"/> and subscribes for change notifications.
    /// </summary>
    /// <remarks>
    /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
    /// </remarks>
    /// <param name="list">The <see cref="IQueryable{T}"/> to observe for changes.</param>
    /// <typeparam name="T">Type of the elements in the list.</typeparam>
    /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
    /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
    /// <returns>
    /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
    /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
    ///
    /// May be null in the case the provided collection is not managed.
    /// </returns>
    public static IDisposable? QueryAsyncWithNotifications<T>(this IQueryable<T> list, NotificationCallbackDelegate<T> callback)
        where T : RealmObjectBase
    {
        // Subscribing to non-managed instances doesn't work.
        // In this usage, the instance may be non-managed in tests.
        if (!(list is IRealmCollection<T> realmCollection))
            return null;

        return QueryAsyncWithNotifications(realmCollection, callback);
    }
    
    /// <summary>
    /// A convenience method that casts <see cref="IList{T}"/> to <see cref="IRealmCollection{T}"/> and subscribes for change notifications.
    /// </summary>
    /// <remarks>
    /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
    /// </remarks>
    /// <param name="list">The <see cref="IList{T}"/> to observe for changes.</param>
    /// <typeparam name="T">Type of the elements in the list.</typeparam>
    /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
    /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
    /// <returns>
    /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
    /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
    ///
    /// May be null in the case the provided collection is not managed.
    /// </returns>
    public static IDisposable? QueryAsyncWithNotifications<T>(this IList<T> list, NotificationCallbackDelegate<T> callback)
        where T : RealmObjectBase
    {
        // Subscribing to non-managed instances doesn't work.
        // In this usage, the instance may be non-managed in tests.
        if (!(list is IRealmCollection<T> realmCollection))
            return null;

        return QueryAsyncWithNotifications(realmCollection, callback);
    }
}
