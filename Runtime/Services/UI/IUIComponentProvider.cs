using System;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// The seam between "a presenter was opened" and "a view exists": produces the
    /// <c>UnityEngine.Component</c> the presenter will be attached to, plus the scope that releases it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the extension point for where a view comes from.</b> Implement it to load from
    /// Addressables instead of <c>Resources</c>, to take an instance from a warm pool, to build a view in
    /// code, or simply to parent under a different <see cref="ITransformProvider"/> layer. The three
    /// built-in implementations — <c>UIWindowComponentProvider</c>, <c>UIHudComponentProvider</c> and
    /// <c>UITooltipComponentProvider</c> — all load a prefab by <see cref="Options.Path"/>.
    /// </para>
    /// <para>
    /// <b>Implementations are injected before every call.</b> Whichever service is opening the view
    /// injects the provider and only then calls <see cref="Provide"/>, so an implementation may declare
    /// <c>[Inject]</c> members and rely on them being filled by the time <see cref="Provide"/> runs — but
    /// must not touch them in its constructor.
    /// </para>
    /// <para>
    /// <b>Instance reuse differs by service, so do not keep per-open state in a field.</b> The window and
    /// HUD services build a provider per open from the factory in <see cref="Options.Provider"/>; the
    /// tooltip service holds one registered instance and re-injects it for every open. An implementation
    /// that has to survive concurrent opens must keep that state in locals and closures instead.
    /// </para>
    /// </remarks>
    public interface IUIComponentProvider
    {
        /// <summary>
        /// Creates the view for one open and reports it through <paramref name="onResult"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Returning means nothing.</b> An implementation may invoke <paramref name="onResult"/> before
        /// this method returns (the prefab was already in memory), or many frames later (a load was in
        /// flight), or never at all (<paramref name="lifetime"/> ended while the load was still running).
        /// Callers must therefore drive everything that depends on the view from inside the callback, and
        /// must tolerate an open that never completes.
        /// </para>
        /// <para>
        /// <b>Implementations must respect the lifetime.</b> Tie the load to <paramref name="lifetime"/>
        /// so a cancelled open instantiates nothing, and nest the returned context's scope in it so the
        /// view cannot outlive what it was opened for.
        /// </para>
        /// </remarks>
        /// <param name="lifetime">The scope of this open, nested in the opening service's own lifetime.
        /// It ends on whichever comes first — the open being closed through its reference, the view's
        /// <c>GameObject</c> being destroyed, or the service ending — and takes the view with it.</param>
        /// <param name="options">The registration's settings; the built-in providers read only
        /// <see cref="Options.Path"/> from it.</param>
        /// <param name="targetType">The component type the caller wants — the presenter's view type. What
        /// is handed back need only lead to it: the UI services accept a different component on the same
        /// <c>GameObject</c> and <c>GetComponent</c> their way to the one they asked for.</param>
        /// <param name="onResult">Called at most once, with the created view and its scope. From that
        /// moment the caller owns the <see cref="UIComponentProviderContext"/> and is responsible for
        /// disposing it.</param>
        void Provide(
            Lifetime lifetime,
            Options options,
            Type targetType,
            Action<UIComponentProviderContext> onResult
        );
    }
}
