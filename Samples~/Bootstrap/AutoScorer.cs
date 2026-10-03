using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Utils;
using UnityEngine;

namespace OpenUGD.Samples.Bootstrap
{
    /// <summary>
    /// A service that needs the Unity loop: it adds a point every second through an injected
    /// <see cref="ICoroutineProvider"/>, so it holds no reference to a <c>MonoBehaviour</c> and a test can hand it a
    /// fake.
    /// </summary>
    public sealed class AutoScorer : IInitializeService
    {
        private readonly ICoroutineProvider _coroutines;
        private readonly Lifetime _lifetime;
        private readonly IScore _score;

        public AutoScorer(IScore score, ICoroutineProvider coroutines, Lifetime lifetime)
        {
            _score = score;
            _coroutines = coroutines;
            _lifetime = lifetime;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            // Started during the boot, which ContextBehaviour runs from Awake; stopped when the context ends.
            var coroutine = _coroutines.StartCoroutine(Tick());
            _lifetime.AddAction(() => _coroutines.StopCoroutine(coroutine));
            return Task.CompletedTask;
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSeconds(1f);
            while (true)
            {
                yield return second;
                _score.Add(1);
            }
        }
    }
}
