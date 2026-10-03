using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenUGD.Core
{
    /// <summary>
    /// What the game's own context on the instance prefab does in 2.0: a <see cref="ContextBehaviour"/> that
    /// registers this instance as <see cref="IContextInstanceProvider"/> and points the instance's cameras at
    /// its display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Put it, or your own context written the same way, on the root of the prefab that carries
    /// <see cref="ContextInstanceComponent"/>. It replaces a 0.6.x <c>ContextFactoryComponent</c> subclass
    /// that registered the instance with <c>Injector.ToValue</c>.
    /// </para>
    /// <para>
    /// <see cref="ContextBehaviour"/> marks its <c>GameObject</c> <c>DontDestroyOnLoad</c> by default (see
    /// <c>PersistAcrossScenes</c>), and the prefab root it sits on is a root object once instantiated, which
    /// agrees with what <see cref="ContextFactoryInstancesComponent"/> does with the instances it creates.
    /// </para>
    /// <para>
    /// Only cameras are retargeted. A canvas in Screen Space - Overlay mode has its own
    /// <c>targetDisplay</c>; set it the same way if the instance has one.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(ContextInstanceComponent))]
    public class InstanceContextExample : ContextBehaviour
    {
        /// <inheritdoc />
        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            var builder = Context.CreateBuilder(Lifetime);
            builder.Services.AddInstance<IContextInstanceProvider>(GetComponent<ContextInstanceComponent>());

            // Register the game's own services here.

            return builder.BuildAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override void OnStarted(Context context)
        {
            var display = context.Resolve<IContextInstanceProvider>().TargetDisplay;
            foreach (var view in GetComponentsInChildren<Camera>(true))
            {
                view.targetDisplay = display;
            }
        }
    }
}
