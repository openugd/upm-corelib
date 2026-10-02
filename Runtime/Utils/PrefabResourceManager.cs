using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace OpenUGD.Utils
{
    /// <summary>
    /// A per-path cache of prefabs loaded from <c>Resources</c>, each with its own pool of instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One <see cref="ResourceResult"/> per path, kept forever.</b> <see cref="GetPrefab(string)"/>
    /// returns the same object for the same string every time, and the cache is never pruned: a path asked
    /// for once is held until the manager is disposed. That is what makes pooling work — releasing an
    /// instance and instantiating again reuses the same object — and it is also why a failed load stays
    /// failed, since the error is recorded on the cached result rather than retried per request.
    /// </para>
    /// <para>
    /// <b>Ownership.</b> The manager owns every <see cref="ResourceResult"/> it creates, and those results
    /// own every <see cref="GameObject"/> they hand out. Callers borrow both: never dispose a result, and
    /// give instances back through <see cref="ResourceResult.Release(GameObject)"/> instead of destroying
    /// them. <see cref="Dispose"/> destroys the lot.
    /// </para>
    /// <para>
    /// <b>The lifetime is not a subscription.</b> The lifetime given to the constructor is forwarded to
    /// <see cref="CreateResourceResult"/> and used for nothing else — the manager registers no clean-up on
    /// it. Terminating it destroys nothing; whoever built the manager must dispose it.
    /// </para>
    /// <para>
    /// <b>Main thread only.</b> Every load and instantiate path reaches <c>Resources</c>,
    /// <c>Object.Instantiate</c> or a coroutine, none of which tolerate a background thread, and the
    /// dictionary, list and stack behind the rest are unsynchronized. No member is thread-safe.
    /// </para>
    /// </remarks>
    public class PrefabResourceManager : IDisposable
    {
        /// <summary>
        /// The provider given to the constructor, exposed for a <see cref="CreateResourceResult"/> override
        /// that needs to build its own result.
        /// </summary>
        protected readonly ICoroutineProvider _coroutineProvider;
        private readonly Lifetime _lifetime;
        private readonly Dictionary<string, ResourceResult> _map;

        /// <summary>
        /// Creates an empty cache. Nothing is loaded until a result obtained from
        /// <see cref="GetPrefab(string)"/> is asked to load or to instantiate.
        /// </summary>
        /// <param name="coroutineProvider">Runs the coroutine behind every asynchronous load. It has to
        /// outlive the manager: when its host is destroyed or deactivated, loads in flight stop where they
        /// are and their callbacks never fire.</param>
        /// <param name="lifetime">Passed on to <see cref="CreateResourceResult"/> for derived results that
        /// want a scope. The manager registers nothing on it, so this is not an auto-dispose.</param>
        public PrefabResourceManager(ICoroutineProvider coroutineProvider, Lifetime lifetime)
        {
            _coroutineProvider = coroutineProvider;
            _lifetime = lifetime;
            _map = new Dictionary<string, ResourceResult>();
        }

        /// <summary>
        /// Destroys every instance and every pooled object of every cached path.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It destroys what callers are still holding</b>, not just what has been given back — see
        /// <see cref="ResourceResult.Dispose"/>. Any <see cref="GameObject"/> reference kept across this
        /// call becomes a destroyed object.
        /// </para>
        /// <para>
        /// The cache itself survives, so the manager stays usable: <see cref="GetPrefab(string)"/> keeps
        /// returning the same results, still holding their loaded prefab assets, and instantiating from one
        /// again works. What changes is that <see cref="ResourceResult.Release(GameObject)"/> becomes a
        /// silent no-op on each of them, so scope-bound teardown that runs after this does not throw.
        /// </para>
        /// <para>
        /// The prefab assets themselves are not unloaded; call <c>Resources.UnloadUnusedAssets</c> if you
        /// need that memory back. Calling this twice is harmless.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            foreach (var value in _map.Values)
            {
                value.Dispose();
            }
        }

        /// <summary>
        /// Returns the cached result for a <c>Resources</c>-relative prefab path, creating it on first
        /// request.
        /// </summary>
        /// <remarks>
        /// Cheap, and free of side effects beyond the cache entry: no file is touched and no path is
        /// validated, so a path that does not exist is indistinguishable from one that does until the
        /// result is asked to load. The lookup is an ordinary case-sensitive dictionary hit, so two
        /// spellings of the same asset produce two results, two loads and two pools.
        /// </remarks>
        /// <param name="prefab">The path as <c>Resources.Load</c> takes it: relative to a <c>Resources</c>
        /// folder, without a file extension.</param>
        /// <returns>The result for that path — never <c>null</c> unless a
        /// <see cref="CreateResourceResult"/> override returned one. Owned by this manager: hold it for as
        /// long as you like, but do not dispose it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="prefab"/> is <c>null</c>; it is used as a
        /// dictionary key.</exception>
        public ResourceResult GetPrefab(string prefab)
        {
            ResourceResult result;

            if (!_map.TryGetValue(prefab, out result))
            {
                result = CreateResourceResult(prefab, _lifetime);
                _map[prefab] = result;
            }

            return result;
        }

        /// <summary>
        /// Creates the result for a path not seen before. Override it to return a subclass — one that loads
        /// from an asset bundle, say — without reimplementing the cache or the pooling.
        /// </summary>
        /// <remarks>
        /// Called at most once per distinct path, from <see cref="GetPrefab(string)"/>, immediately before
        /// the entry is cached. Whatever it returns is stored as-is and never replaced, so returning
        /// <c>null</c> caches a <c>null</c> for that path for the life of the manager.
        /// </remarks>
        /// <param name="prefab">The path being cached, exactly as it was passed to
        /// <see cref="GetPrefab(string)"/>.</param>
        /// <param name="lifetime">The lifetime given to this manager's constructor. The base result ignores
        /// it; an override is free not to.</param>
        /// <returns>The result to cache for <paramref name="prefab"/>.</returns>
        protected virtual ResourceResult CreateResourceResult(string prefab, Lifetime lifetime) =>
            new(_coroutineProvider, prefab, lifetime);

        /// <summary>
        /// Empties every pool: destroys the instances that were released and not taken out again, and
        /// leaves the ones still on loan alone.
        /// </summary>
        /// <remarks>
        /// The memory-pressure release valve, and the only cheap one here — nothing is unloaded and no
        /// cache entry is dropped, so the next instantiate of a collected path pays for a real
        /// <c>Object.Instantiate</c> instead of a pool pop, and nothing else changes.
        /// </remarks>
        public void Collect()
        {
            foreach (var resource in _map.Values)
            {
                resource.Collect();
            }
        }

        /// <summary>
        /// A prefab path paired with a progress value, for reporting the state of a load onwards.
        /// </summary>
        /// <remarks>
        /// A plain carrier with no behaviour and no producer: nothing in this class creates, fills or
        /// consumes one, and it is not wired to <see cref="ResourceResult.Progress"/>. It exists so that
        /// code aggregating several loads into one progress bar has a shape to pass around.
        /// </remarks>
        public struct ResourceLoadProgress
        {
            /// <summary>
            /// The <c>Resources</c>-relative path this reading is about.
            /// </summary>
            public string PrefabPath;

            /// <summary>
            /// How far that load has got, on the 0-to-1 scale Unity's load requests report.
            /// </summary>
            public float Progress;
        }

        /// <summary>
        /// One prefab path: its load state, the asset once loaded, the instances handed out from it, and
        /// the pool of instances given back.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Obtained, not constructed.</b> You normally get one from
        /// <see cref="PrefabResourceManager.GetPrefab(string)"/>, which owns it and disposes it. The usual
        /// shape is load, instantiate, and tie the release to a scope:
        /// </para>
        /// <code>
        /// manager.GetPrefab(path).LoadAsync(lifetime, result => {
        ///     var view = result.Instantiate&lt;MyView&gt;(parent);
        ///     lifetime.AddAction(() => result.Release(view));
        /// });
        /// </code>
        /// <para>
        /// <b>Releasing does not reset.</b> An instance handed back is pushed onto the pool exactly as it
        /// is — still active, still parented where it was, with whatever state its components hold — and
        /// handed out again in that state. Only the overloads taking a <see cref="Transform"/> reparent;
        /// nothing ever deactivates or clears state. Parking a released object somewhere harmless is the
        /// caller's job.
        /// </para>
        /// <para>
        /// <b>A missing path fails quietly, and differently depending on how you asked.</b>
        /// <see cref="Load"/> sets <see cref="IsError"/> and leaves <see cref="IsCompleted"/> exactly as it
        /// found it — <c>false</c> on a result nothing has loaded yet; the asynchronous overloads set both
        /// and then invoke their callbacks with <see cref="Prefab"/> still <c>null</c>. Since
        /// <see cref="IsCompleted"/> is exactly what the callback overloads test before doing any work, a
        /// path that failed asynchronously is never loaded again by them — every later call reports the
        /// same failure immediately. <see cref="LoadAsync()"/> is the one way to retry. The instantiate
        /// methods are the only members that raise a missing path as an exception.
        /// </para>
        /// <para>
        /// <b>Logging.</b> Every asynchronous load writes one line to the Unity console through
        /// <c>Debug.Log</c> — naming the loaded prefab when it finishes, or reporting the abandoned load
        /// if <see cref="Dispose"/> cuts it short first. This is unconditional; the synchronous
        /// <see cref="Load"/> is silent.
        /// </para>
        /// </remarks>
        public class ResourceResult : IDisposable
        {
            private readonly ICoroutineProvider _coroutineProvider;

            private readonly List<GameObject> _instances;
            private readonly Stack<GameObject> _pool;
            private readonly List<Action<ResourceResult>> _onResult;
            private readonly string _prefabPath;
            private Coroutine _coroutine;
            private ResourceRequest _loadAsync;
            private bool _unload;

            /// <summary>
            /// Creates an empty result for a path, loading nothing.
            /// </summary>
            /// <remarks>
            /// Call this only from a <see cref="PrefabResourceManager.CreateResourceResult"/> override.
            /// Everywhere else go through <see cref="PrefabResourceManager.GetPrefab(string)"/>, so that a
            /// path keeps one pool: two results over the same path share nothing, and an instance released
            /// to one cannot be handed out by the other.
            /// </remarks>
            /// <param name="coroutineProvider">Runs the coroutine behind the <c>LoadAsync</c> overloads.
            /// The synchronous paths never touch it.</param>
            /// <param name="prefab">The path as <c>Resources.Load</c> takes it: relative to a
            /// <c>Resources</c> folder, without a file extension. Never validated, and fixed for the life
            /// of the result.</param>
            /// <param name="lifetime">Accepted for the benefit of derived results; this class neither
            /// stores it nor registers anything on it, so terminating it destroys nothing and cancels
            /// nothing. Disposal is the owner's job.</param>
            public ResourceResult(ICoroutineProvider coroutineProvider, string prefab, Lifetime lifetime)
            {
                _instances = new List<GameObject>();
                _pool = new Stack<GameObject>();
                _coroutineProvider = coroutineProvider;
                _prefabPath = prefab;
                _onResult = new List<Action<ResourceResult>>(1);
            }

            /// <summary>
            /// How far the asynchronous load has got, as last polled by the driving coroutine.
            /// </summary>
            /// <remarks>
            /// Only the <c>LoadAsync</c> overloads move it, once per frame while a load is in flight. It is
            /// not reset to 0 when a new load starts and not snapped to 1 when one finishes, so it is a
            /// progress-bar input and nothing more — ask <see cref="IsCompleted"/> whether the load is done.
            /// A synchronous <see cref="Load"/> never touches it, so a result loaded that way reads 0.
            /// </remarks>
            public float Progress { get; private set; }

            /// <summary>
            /// Whether a load has finished. <b>Not</b> whether it succeeded: an asynchronous load of a
            /// missing path finishes with this <c>true</c> and <see cref="IsError"/> true as well.
            /// </summary>
            /// <remarks>
            /// This is the latch the callback overloads test — while it is <c>true</c> they invoke the
            /// callback at once instead of loading anything, which is what makes a repeated
            /// <c>GetPrefab(...).LoadAsync(...)</c> cheap and a failed path permanently failed. It returns
            /// to <c>false</c> when a load starts and when a load in flight is cut short by
            /// <see cref="Dispose"/>. <see cref="Load"/> sets it only when the asset actually loaded, and
            /// the instantiate methods never set it even though they may load the prefab.
            /// </remarks>
            public bool IsCompleted { get; private set; }

            /// <summary>
            /// Whether the last load found nothing at <see cref="PrefabPath"/>.
            /// </summary>
            /// <remarks>
            /// Check it inside a load callback before touching <see cref="Prefab"/>: a load that set this
            /// left the prefab <c>null</c>. It is cleared only when one of the <c>LoadAsync</c> overloads
            /// starts a fresh load; <see cref="Load"/> sets it and never clears it.
            /// </remarks>
            public bool IsError { get; private set; }

            /// <summary>
            /// How many instances are on loan — instantiated from this result and not yet handed back
            /// through <see cref="Release(GameObject)"/>.
            /// </summary>
            /// <remarks>
            /// It counts what this result gave out, not what still exists: an instance somebody destroyed
            /// behind its back is still counted, and pooled instances are not counted at all. Useful as a
            /// leak assertion at a scene boundary, where it should be back to 0.
            /// </remarks>
            public int Instances => _instances.Count;

            /// <summary>
            /// The loaded prefab asset, or <c>null</c> until something loads it.
            /// </summary>
            /// <remarks>
            /// Set by a successful load and by the first instantiate, which loads synchronously if it has
            /// to. This is the shared asset, not a copy — instantiate from it, never mutate it.
            /// <see cref="Dispose"/> leaves it set, so the asset stays in memory; only a load interrupted
            /// by <see cref="Dispose"/> clears it.
            /// </remarks>
            public GameObject Prefab { get; private set; }

            /// <summary>
            /// The path this result loads, exactly as it was passed in. It never changes: a result is bound
            /// to one path for its whole life.
            /// </summary>
            public string PrefabPath => _prefabPath;

            /// <summary>
            /// Destroys every instance this result handed out and everything in its pool, and marks the
            /// result unloaded.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <b>Instances on loan are not exempt.</b> Every <see cref="GameObject"/> in both lists goes
            /// to <c>Object.Destroy</c> — at the end of the frame, as always — and both lists are
            /// emptied, so references callers are still holding become destroyed objects.
            /// </para>
            /// <para>
            /// <b>Afterwards</b> <see cref="Release(GameObject)"/> is a silent no-op, which is what lets
            /// scope-bound teardown run safely after the manager has gone. <see cref="Prefab"/>,
            /// <see cref="IsCompleted"/> and <see cref="IsError"/> all survive, so instantiating again
            /// works and hands out new objects — which cannot be released while the flag stands. Only a
            /// load that actually starts clears it, and on a result that had already completed the
            /// callback overloads short-circuit on <see cref="IsCompleted"/> without starting one, so
            /// <see cref="LoadAsync()"/> is what brings <c>Release</c> back.
            /// </para>
            /// <para>
            /// <b>A load in flight</b> stops at its next frame, clears <see cref="Prefab"/> and
            /// <see cref="IsCompleted"/>, and does not invoke its callbacks — they stay registered and fire
            /// whenever a later load completes.
            /// </para>
            /// </remarks>
            public void Dispose()
            {
                _unload = true;
                if (_instances.Count != 0)
                {
                    foreach (var gameObject in _instances)
                    {
                        Object.Destroy(gameObject);
                    }

                    _instances.Clear();
                }

                if (_pool.Count != 0)
                {
                    foreach (var gameObject in _pool)
                    {
                        Object.Destroy(gameObject);
                    }

                    _pool.Clear();
                }
            }

            /// <summary>
            /// Takes an instance from the pool, or makes one, and returns the <typeparamref name="T"/> on
            /// its root object.
            /// </summary>
            /// <remarks>
            /// <para>
            /// A pooled object comes back exactly as it was released: same parent, same active state, same
            /// component state. Nothing is reset and nothing is reparented — use
            /// <see cref="Instantiate{T}(Transform)"/> when the instance needs a home.
            /// </para>
            /// <para>
            /// With an empty pool and no prefab loaded yet this loads the asset <b>synchronously</b> and
            /// stalls the frame; load first if that matters. The synchronous load does not set
            /// <see cref="IsCompleted"/>, so a later <c>LoadAsync</c> still does a load of its own.
            /// </para>
            /// </remarks>
            /// <typeparam name="T">A component on the prefab's root object. Children are not searched, and
            /// the type is not checked against the prefab until this runs.</typeparam>
            /// <returns>The component on the instance. Counted in <see cref="Instances"/> until
            /// <see cref="Release(GameObject)"/> or <see cref="Release(Component)"/> takes it back; owned by
            /// this result either way, so release it rather than destroying it.</returns>
            /// <exception cref="InvalidOperationException">Nothing could be loaded from
            /// <see cref="PrefabPath"/>. The message carries the path.</exception>
            /// <exception cref="NullReferenceException">The instance's root has no
            /// <typeparamref name="T"/>.</exception>
            public T Instantiate<T>() where T : Component
            {
                T instance;
                if (_pool.Count != 0)
                {
                    instance = _pool.Pop().GetComponent<T>();
                }
                else
                {
                    var prefab = GetPrefab();
                    if (prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"The Object you want to instantiate is null., prefab path: {_prefabPath}");
                    }

                    instance = Object.Instantiate(prefab).GetComponent<T>();
                }

                _instances.Add(instance.gameObject);
                return instance;
            }

            /// <summary>
            /// Takes an instance from the pool, or makes one, and returns the object itself.
            /// </summary>
            /// <remarks>
            /// The cheapest of the five overloads and the least helpful: a pooled object is handed back
            /// untouched — same parent, same active state — and a fresh one is created at the scene root
            /// with the prefab's own transform values. With an empty pool and no prefab loaded yet this
            /// loads the asset <b>synchronously</b>, stalling the frame, without setting
            /// <see cref="IsCompleted"/>.
            /// </remarks>
            /// <returns>The instance. Counted in <see cref="Instances"/> until
            /// <see cref="Release(GameObject)"/> takes it back; owned by this result, so release it rather
            /// than destroying it.</returns>
            /// <exception cref="InvalidOperationException">Nothing could be loaded from
            /// <see cref="PrefabPath"/>. The message carries the path.</exception>
            public GameObject Instantiate()
            {
                GameObject instance;
                if (_pool.Count != 0)
                {
                    instance = _pool.Pop();
                }
                else
                {
                    var prefab = GetPrefab();
                    if (prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"The Object you want to instantiate is null., prefab path: {_prefabPath}");
                    }

                    instance = Object.Instantiate(prefab);
                }

                _instances.Add(instance);
                return instance;
            }

            /// <summary>
            /// Takes an instance from the pool, or makes one, parents it under
            /// <paramref name="transform"/> and returns the <typeparamref name="T"/> on its root object.
            /// </summary>
            /// <remarks>
            /// The overload to reach for: like the other two that take a <see cref="Transform"/>, it puts
            /// the instance somewhere definite instead of leaving a pooled object wherever it was last
            /// released. Parenting keeps local position, rotation and scale rather than world ones, which
            /// is what UI wants — the instance lands where the parent puts it. Everything else about a
            /// pooled object is left as it was released, including its active state.
            /// </remarks>
            /// <typeparam name="T">A component on the prefab's root object. Children are not
            /// searched.</typeparam>
            /// <param name="transform">The parent to put the instance under.</param>
            /// <returns>The component on the instance. Counted in <see cref="Instances"/> until
            /// <see cref="Release(GameObject)"/> or <see cref="Release(Component)"/> takes it back.</returns>
            /// <exception cref="InvalidOperationException">Nothing could be loaded from
            /// <see cref="PrefabPath"/>. The message carries the path.</exception>
            /// <exception cref="NullReferenceException">The instance's root has no
            /// <typeparamref name="T"/>.</exception>
            public T Instantiate<T>(Transform transform) where T : Component =>
                (T)Instantiate(typeof(T), transform);

            /// <summary>
            /// The reflective form of <see cref="Instantiate{T}(Transform)"/>, for callers that only learn
            /// the component type at run time — a UI service told which view class to open, say.
            /// </summary>
            /// <remarks>
            /// Identical in every other respect, including the parenting rule: local transform values are
            /// preserved, and a pooled object keeps the state it was released in.
            /// </remarks>
            /// <param name="type">The component type to take from the instance's root object.</param>
            /// <param name="transform">The parent to put the instance under.</param>
            /// <returns>The component on the instance, typed as <see cref="Component"/>. Counted in
            /// <see cref="Instances"/> until it is released.</returns>
            /// <exception cref="InvalidOperationException">Nothing could be loaded from
            /// <see cref="PrefabPath"/>. The message carries the path.</exception>
            /// <exception cref="NullReferenceException">The instance's root has no
            /// <paramref name="type"/>.</exception>
            public Component Instantiate(Type type, Transform transform)
            {
                Component instance;
                if (_pool.Count != 0)
                {
                    instance = _pool.Pop().GetComponent(type);
                    instance.transform.SetParent(transform, false);
                }
                else
                {
                    var prefab = GetPrefab();
                    if (prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"The Object you want to instantiate is null., prefab path: {_prefabPath}");
                    }

                    instance = ((GameObject)Object.Instantiate((Object)prefab, transform, false)).GetComponent(type);
                }

                _instances.Add(instance.gameObject);
                return instance;
            }

            /// <summary>
            /// Takes an instance from the pool, or makes one, parents it under
            /// <paramref name="transform"/> and returns the object itself.
            /// </summary>
            /// <remarks>
            /// Parenting keeps local position, rotation and scale rather than world ones. A pooled object
            /// is otherwise untouched: its active state and component state are whatever they were when it
            /// was released.
            /// </remarks>
            /// <param name="transform">The parent to put the instance under.</param>
            /// <returns>The instance. Counted in <see cref="Instances"/> until
            /// <see cref="Release(GameObject)"/> takes it back.</returns>
            /// <exception cref="InvalidOperationException">Nothing could be loaded from
            /// <see cref="PrefabPath"/>. The message carries the path.</exception>
            public GameObject Instantiate(Transform transform)
            {
                GameObject instance;
                if (_pool.Count != 0)
                {
                    instance = _pool.Pop();
                    instance.transform.SetParent(transform, false);
                }
                else
                {
                    var prefab = GetPrefab();
                    if (prefab == null)
                    {
                        throw new InvalidOperationException(
                            $"The Object you want to instantiate is null., prefab path: {_prefabPath}");
                    }

                    instance = Object.Instantiate(prefab, transform, false);
                }

                _instances.Add(instance.gameObject);
                return instance;
            }

            /// <summary>
            /// Destroys the pooled instances and empties the pool. Instances on loan are untouched.
            /// </summary>
            /// <remarks>
            /// Entries already destroyed from outside are skipped, so this is safe on a pool that outlived
            /// a scene load. The prefab asset stays loaded and the result stays usable — the next
            /// instantiate simply pays for a real <c>Object.Instantiate</c> again.
            /// </remarks>
            public void Collect()
            {
                foreach (var gameObject in _pool)
                {
                    if (gameObject != null)
                    {
                        Object.Destroy(gameObject);
                    }
                }

                _pool.Clear();
            }

            /// <summary>
            /// Whether <paramref name="value"/> is one of the instances this result currently has on loan.
            /// </summary>
            /// <remarks>
            /// A linear scan of the loan list, so O(<see cref="Instances"/>) — fine for a guard, wrong for
            /// a hot loop. <c>false</c> for a pooled object, for an instance of the same prefab obtained
            /// from a different result, and for anything already released or disposed.
            /// </remarks>
            /// <param name="value">The object to look for. <c>null</c> is allowed and answers
            /// <c>false</c>.</param>
            /// <returns><c>true</c> if <see cref="Release(GameObject)"/> would accept it.</returns>
            public bool IsInstantiated(GameObject value) => _instances.Contains(value);

            /// <summary>
            /// Whether <paramref name="gameObject"/> is sitting in the pool, waiting to be handed out
            /// again.
            /// </summary>
            /// <remarks>
            /// A linear scan of the pool. Not the complement of <see cref="IsInstantiated"/>: both are
            /// <c>false</c> for an object this result never created, and for one that has been destroyed
            /// out of both lists by <see cref="Collect"/> or <see cref="Dispose"/>.
            /// </remarks>
            /// <param name="gameObject">The object to look for. <c>null</c> is allowed and answers
            /// <c>false</c>.</param>
            /// <returns><c>true</c> if it is pooled.</returns>
            public bool IsReleased(GameObject gameObject) => _pool.Contains(gameObject);

            /// <summary>
            /// Takes an instance back and pushes it onto the pool, where the next instantiate of this path
            /// will find it.
            /// </summary>
            /// <remarks>
            /// <para>
            /// The object is <b>not</b> reset: not deactivated, not reparented, not touched at all. It
            /// stays where it is in the scene and is handed out again in that state, so most callers move
            /// it somewhere out of sight first.
            /// </para>
            /// <para>
            /// <b>After <see cref="Dispose"/> this does nothing at all</b> — not even the argument checks
            /// below run — which is what lets scope-bound teardown such as
            /// <c>lifetime.AddAction(() =&gt; result.Release(view))</c> survive a manager that has already
            /// been disposed. Only a load that actually starts clears that flag; on a result that had
            /// already completed the callback overloads short-circuit without starting one, so
            /// <see cref="LoadAsync()"/> is what makes the checks live again.
            /// </para>
            /// </remarks>
            /// <param name="value">An instance this result handed out and has not taken back.</param>
            /// <exception cref="NullReferenceException"><paramref name="value"/> is <c>null</c>. Thrown
            /// deliberately, naming the path, rather than putting a null into the pool.</exception>
            /// <exception cref="ArgumentException"><paramref name="value"/> is already pooled (a double
            /// release), or was never handed out by this result. Both would otherwise surface much later as
            /// two callers holding the same object.</exception>
            public void Release(GameObject value)
            {
                if (_unload)
                {
                    return;
                }

                if (value == null)
                {
                    throw new NullReferenceException("value can't be null:" + _prefabPath);
                }

                if (_pool.Contains(value))
                {
                    throw new ArgumentException("pool contains this gameObject: " + _prefabPath);
                }

                if (!_instances.Contains(value))
                {
                    throw new ArgumentException("gameObject not in instance list: " + _prefabPath +
                                                ", value in pool: " + _pool.Contains(value));
                }

                _instances.Remove(value);

                _pool.Push(value);
            }

            /// <summary>
            /// Releases the object a component sits on, for the common case where you kept the view rather
            /// than its <see cref="GameObject"/>.
            /// </summary>
            /// <remarks>
            /// Behaves exactly like <see cref="Release(GameObject)"/>, including being a no-op once the
            /// manager has been unloaded. It applies its own guards before reading
            /// <c>value.gameObject</c>, because a destroyed component cannot answer for its object:
            /// Unity's <c>operator ==</c> reports a destroyed component as <c>null</c>, so a destroyed
            /// instance takes the same path as a null one instead of throwing
            /// <c>MissingReferenceException</c> out of the dereference.
            /// </remarks>
            /// <param name="value">A component on an instance this result handed out.</param>
            /// <exception cref="NullReferenceException"><paramref name="value"/> is <c>null</c> or has
            /// already been destroyed.</exception>
            /// <exception cref="ArgumentException">Same two cases as
            /// <see cref="Release(GameObject)"/>: already pooled, or never handed out here.</exception>
            public void Release(Component value)
            {
                if (_unload)
                {
                    return;
                }

                if (value == null)
                {
                    throw new NullReferenceException("value can't be null:" + _prefabPath);
                }

                Release(value.gameObject);
            }

            /// <summary>
            /// Loads the prefab synchronously, stalling the frame, and records the outcome in the flags.
            /// </summary>
            /// <remarks>
            /// <para>
            /// Reads nothing from disk if <see cref="Prefab"/> is already in memory, but still settles the
            /// flags. On success <see cref="IsCompleted"/> becomes <c>true</c>; on failure
            /// <see cref="IsError"/> becomes <c>true</c> and <see cref="IsCompleted"/> is <b>left as it
            /// was</b> — the one finished load in this class that does not set it. Nothing is thrown
            /// either way, so read the flags.
            /// </para>
            /// <para>
            /// Callbacks registered through the <c>LoadAsync</c> overloads are not invoked; this path knows
            /// nothing about them. A load already in flight is not cancelled either, and will overwrite
            /// <see cref="Prefab"/> with its own result when it finishes.
            /// </para>
            /// </remarks>
            /// <returns>This result, so the call can be chained straight into an instantiate.</returns>
            public ResourceResult Load()
            {
                var prefab = GetPrefab();
                if (prefab != null)
                {
                    IsCompleted = true;
                }
                else
                {
                    IsError = true;
                }

                return this;
            }

            /// <summary>
            /// Loads the prefab in the background and calls <paramref name="onResult"/> when it settles,
            /// unless <paramref name="lifetime"/> ends first. The overload to prefer.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <b>The callback can run before this returns.</b> When <see cref="IsCompleted"/> is already
            /// <c>true</c> — including after a load that failed — it is invoked synchronously, and
            /// <paramref name="lifetime"/> is not consulted at all, so it fires even for a lifetime that
            /// has already terminated. Write the callback so it does not mind either timing.
            /// </para>
            /// <para>
            /// <b>Cancellation is a de-registration, not an abort.</b> Terminating
            /// <paramref name="lifetime"/> removes this callback; the load itself carries on and completes
            /// for everyone else waiting on the same path. Because a terminated lifetime runs a newly added
            /// action immediately, passing one that has already terminated registers and removes the
            /// callback in the same breath: the load still starts, and this caller never hears about it.
            /// </para>
            /// <para>
            /// <b>Failure still calls back.</b> A missing path invokes the callback with
            /// <see cref="IsError"/> <c>true</c> and <see cref="Prefab"/> <c>null</c>, and marks the result
            /// completed — so every later call on that path calls back immediately with the same failure.
            /// There is no retry here; <see cref="LoadAsync()"/> is the only way to force one.
            /// </para>
            /// <para>
            /// <b>Ordering.</b> Callbacks run in registration order, over a private snapshot; the
            /// registration list is emptied before the first of them runs, so a callback may register
            /// another one without disturbing the pass in progress — the new one waits for the next load.
            /// </para>
            /// </remarks>
            /// <param name="lifetime">Scopes the subscription: when it terminates the callback is dropped.
            /// Required only on the path where the load has not already completed.</param>
            /// <param name="onResult">Invoked at most once, with this result, whether the load succeeded or
            /// failed. It is dropped if the lifetime ends first, and deferred to the next load if
            /// <see cref="Dispose"/> interrupts this one.</param>
            /// <returns>This result, for chaining.</returns>
            /// <exception cref="NullReferenceException"><paramref name="onResult"/> is <c>null</c> and the
            /// result is already loaded, or <paramref name="lifetime"/> is <c>null</c> and it is
            /// not.</exception>
            /// <exception cref="InvalidOperationException">The coroutine provider cannot start a coroutine
            /// — its host has been destroyed or deactivated.</exception>
            public ResourceResult LoadAsync(Lifetime lifetime, Action<ResourceResult> onResult)
            {
                if (IsCompleted)
                {
                    onResult(this);
                }
                else
                {
                    _onResult.Add(onResult);
                    lifetime.AddAction(() => _onResult.Remove(onResult));

                    if (_loadAsync == null)
                    {
                        IsCompleted = false;
                        IsError = false;
                        _unload = false;
                        _loadAsync = Resources.LoadAsync<GameObject>(_prefabPath);
                        StopCoroutine();
                        _coroutine = _coroutineProvider.StartCoroutine(LoadAsyncProcess());
                    }
                }

                return this;
            }

            /// <summary>
            /// Loads the prefab in the background and calls <paramref name="onResult"/> when it settles,
            /// with no way to cancel.
            /// </summary>
            /// <remarks>
            /// Behaves exactly like <see cref="LoadAsync(Lifetime, Action{ResourceResult})"/> — same
            /// synchronous shortcut when the result is already loaded, same call-back-on-failure, same
            /// ordering — minus the escape hatch: the callback stays registered until a load completes, so
            /// it runs even when everything it captured has gone away. Use this only when the callback
            /// touches nothing that can be closed.
            /// </remarks>
            /// <param name="onResult">Invoked at most once, with this result, whether the load succeeded or
            /// failed.</param>
            /// <returns>This result, for chaining.</returns>
            /// <exception cref="NullReferenceException"><paramref name="onResult"/> is <c>null</c> and the
            /// result is already loaded. A <c>null</c> registered against a load in flight fails later,
            /// inside the coroutine.</exception>
            /// <exception cref="InvalidOperationException">The coroutine provider cannot start a coroutine
            /// — its host has been destroyed or deactivated.</exception>
            public ResourceResult LoadAsync(Action<ResourceResult> onResult)
            {
                if (IsCompleted)
                {
                    onResult(this);
                }
                else
                {
                    _onResult.Add(onResult);

                    if (_loadAsync == null)
                    {
                        IsCompleted = false;
                        IsError = false;
                        _unload = false;
                        _loadAsync = Resources.LoadAsync<GameObject>(_prefabPath);
                        StopCoroutine();
                        _coroutine = _coroutineProvider.StartCoroutine(LoadAsyncProcess());
                    }
                }

                return this;
            }

            /// <summary>
            /// Starts a background load and returns at once, with no callback: poll
            /// <see cref="IsCompleted"/> and <see cref="IsError"/>, or watch <see cref="Progress"/>.
            /// </summary>
            /// <remarks>
            /// Unlike the callback overloads this does not check <see cref="IsCompleted"/> first, so on a
            /// result that has already settled it clears the flags and loads the asset again — which makes
            /// it the only way to retry a path that failed. A load already in flight is left alone, so two
            /// loads of one path can never be stacked. Callbacks registered earlier and still waiting are
            /// invoked by this load when it completes.
            /// </remarks>
            /// <returns>This result, for chaining.</returns>
            /// <exception cref="InvalidOperationException">The coroutine provider cannot start a coroutine
            /// — its host has been destroyed or deactivated.</exception>
            public ResourceResult LoadAsync()
            {
                if (_loadAsync == null)
                {
                    IsCompleted = false;
                    IsError = false;
                    _unload = false;
                    _loadAsync = Resources.LoadAsync<GameObject>(_prefabPath);
                    StopCoroutine();
                    _coroutine = _coroutineProvider.StartCoroutine(LoadAsyncProcess());
                }

                return this;
            }

            private IEnumerator LoadAsyncProcess()
            {
                while (!_loadAsync.isDone)
                {
                    if (_unload)
                    {
                        _loadAsync = null;

                        Debug.Log("ResourceResult.LoadAsyncProcess set null to prefab");
                        Prefab = null;
                        IsCompleted = false;
                        yield break;
                    }

                    Progress = _loadAsync.progress;
                    yield return null;
                }

                HandleComplete(_loadAsync.asset as GameObject);

                _loadAsync = null;
                if (Prefab == null)
                {
                    HandleError();
                }

                var pool = ListPool<Action<ResourceResult>>.Get();
                pool.AddRange(_onResult);
                _onResult.Clear();
                foreach (var action in pool)
                {
                    action(this);
                }

                ListPool<Action<ResourceResult>>.Release(pool);
            }

            private void HandleComplete(GameObject prefab)
            {
                IsCompleted = true;
                Prefab = prefab;

                Debug.Log($"ResourceResult.HandleComplete {Prefab}");
            }

            private void HandleError() => IsError = true;

            private void StopCoroutine()
            {
                if (_coroutine != null)
                {
                    _coroutineProvider.StopCoroutine(_coroutine);
                }
            }

            private GameObject GetPrefab() => Prefab ?? (Prefab = Resources.Load<GameObject>(_prefabPath));

            /// <summary>
            /// A one-line dump for logs and the debugger: path, loaded asset, and both state flags.
            /// </summary>
            /// <returns>A diagnostic string whose format is not part of the contract — read it, do not
            /// parse it.</returns>
            public override string ToString() =>
                $"[Resource Name {PrefabPath}, Prefab {Prefab}, Completed {IsCompleted}, Error {IsError}]";
        }
    }
}
