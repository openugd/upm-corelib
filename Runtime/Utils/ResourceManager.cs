using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenUGD.Utils
{
    public class ResourceManager : IDisposable
    {
        private readonly ICoroutineProvider _coroutineProvider;
        private readonly Dictionary<string, ResourceResult> _map;

        public ResourceManager(ICoroutineProvider coroutineProvider)
        {
            _coroutineProvider = coroutineProvider;
            _map = new Dictionary<string, ResourceResult>();
        }

        public void Dispose()
        {
            foreach (var value in _map.Values)
            {
                value.Dispose();
            }
        }

        public ResourceResult<T> Get<T>(string prefab) where T : Object
        {
            ResourceResult result;
            if (!_map.TryGetValue(prefab, out result))
            {
                result = new ResourceResult<T>(_coroutineProvider, prefab);
                _map[prefab] = result;
            }

            return (ResourceResult<T>)result;
        }

        public abstract class ResourceResult : IDisposable
        {
            public abstract float Progress { get; }
            public abstract bool IsCompleted { get; }
            public abstract bool IsError { get; }

            public virtual void Dispose()
            {
            }

            public abstract void Collect();
        }

        public class ResourceResult<T> : ResourceResult where T : Object
        {
            private readonly ICoroutineProvider _coroutineProvider;
            private readonly Signal<ResourceResult<T>> _onResult;
            private readonly string _path;
            private Coroutine _coroutine;
            private bool _isCompleted;
            private bool _isError;
            private ResourceRequest _loadAsync;
            private float _progress;
            private bool _unload;


            public ResourceResult(ICoroutineProvider coroutineProvider, string path)
            {
                _coroutineProvider = coroutineProvider;
                _path = path;
                _onResult = new Signal<ResourceResult<T>>(Lifetime.Eternal);
            }

            public override float Progress => _progress;
            public override bool IsCompleted => _isCompleted;
            public override bool IsError => _isError;
            public T Result { get; private set; }

            public override void Collect()
            {
                if (Result != null)
                {
                    Resources.UnloadAsset(Result);
                    Result = null;
                }

                _isCompleted = false;
                _isError = false;
            }

            public override void Dispose()
            {
                _unload = true;
                Collect();
            }

            public ResourceResult<T> LoadAsync(Lifetime lifetime, Action<ResourceResult<T>> onResult)
            {
                if (!lifetime.IsTerminated)
                {
                    _onResult.Subscribe(lifetime, onResult);
                }

                if (IsCompleted)
                {
                    FireOnResult();
                }
                else
                {
                    if (_loadAsync == null)
                    {
                        _isCompleted = false;
                        _isError = false;
                        _unload = false;
                        _loadAsync = Resources.LoadAsync<T>(_path);
                        StopCoroutine();
                        _coroutine = _coroutineProvider.StartCoroutine(LoadAsyncProcess());
                    }
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
                        Result = null;
                        _isCompleted = false;
                        yield break;
                    }

                    _progress = _loadAsync.progress;
                    yield return null;
                }

                _isCompleted = true;
                Result = _loadAsync.asset as T;
                _loadAsync = null;
                if (Result == null)
                {
                    _isError = true;
                }

                FireOnResult();
            }

            private void StopCoroutine()
            {
                if (_coroutine != null)
                {
                    _coroutineProvider.StopCoroutine(_coroutine);
                }
            }

            private void FireOnResult() => _onResult.Fire(this);
        }
    }
}
