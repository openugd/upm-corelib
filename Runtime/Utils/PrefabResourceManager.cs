﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace OpenUGD.Utils
{
    public class PrefabResourceManager : IDisposable
    {
        protected readonly ICoroutineProvider _coroutineProvider;
        private readonly Lifetime _lifetime;
        private readonly Dictionary<string, ResourceResult> _map;

        public PrefabResourceManager(ICoroutineProvider coroutineProvider, Lifetime lifetime)
        {
            _coroutineProvider = coroutineProvider;
            _lifetime = lifetime;
            _map = new Dictionary<string, ResourceResult>();
        }

        public void Dispose()
        {
            foreach (var value in _map.Values)
            {
                value.Dispose();
            }
        }

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

        protected virtual ResourceResult CreateResourceResult(string prefab, Lifetime lifetime) =>
            new(_coroutineProvider, prefab, lifetime);

        public void Collect()
        {
            foreach (var resource in _map.Values)
            {
                resource.Collect();
            }
        }

        public struct ResourceLoadProgress
        {
            public string PrefabPath;
            public float Progress;
        }

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

            public ResourceResult(ICoroutineProvider coroutineProvider, string prefab, Lifetime lifetime)
            {
                _instances = new List<GameObject>();
                _pool = new Stack<GameObject>();
                _coroutineProvider = coroutineProvider;
                _prefabPath = prefab;
                _onResult = new List<Action<ResourceResult>>(1);
            }

            public float Progress { get; private set; }
            public bool IsCompleted { get; private set; }
            public bool IsError { get; private set; }
            public int Instances => _instances.Count;
            public GameObject Prefab { get; private set; }
            public string PrefabPath => _prefabPath;

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

            public T Instantiate<T>(Transform transform) where T : Component =>
                (T)Instantiate(typeof(T), transform);

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

            public bool IsInstantiated(GameObject value) => _instances.Contains(value);

            public bool IsReleased(GameObject gameObject) => _pool.Contains(gameObject);

            public void Release(GameObject value)
            {
                if (_unload)
                {
                    return;
                }

                if (value == null)
                {
                    throw new NullReferenceException("value can't bee null:" + _prefabPath);
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

            public void Release(Component value) => Release(value.gameObject);

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

            public override string ToString() =>
                $"[Resource Name {PrefabPath}, Prefab {Prefab}, Completed {IsCompleted}, Error {IsError}]";
        }
    }
}
