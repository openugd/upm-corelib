using System;
using System.Collections.Generic;

namespace OpenUGD.Core.Presenters
{

    /// <summary>
    /// The non-generic face of a presenter that has a view. Implemented explicitly by
    /// <see cref="Presenter{TView}"/>; the typed members are public on that class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two members, both of which exist for exactly one caller shape: a presenter-opening service that knows
    /// the presenter only as a <see cref="Presenter"/>, has to find out what component to load
    /// (<see cref="ViewType"/>), and then has to hand it over (<see cref="SetView"/>).
    /// </para>
    /// <para>
    /// <i>Changed in 2.0.0</i> — <c>object View</c>, <c>OnViewAdded</c>, <c>OnViewBeforeRemove</c> and
    /// <c>OnViewAfterRemoved</c> were removed. They had no caller through this interface: the hooks are
    /// <c>protected virtual</c> members of <see cref="Presenter{TView}"/> and are invoked by the class, so
    /// declaring them here only published a way to fire a presenter's own lifecycle callbacks from outside
    /// it.
    /// </para>
    /// </remarks>
    public interface IPresenterWithView
    {
        /// <summary>
        /// The type this presenter's view must be assignable to — the <c>TView</c> of
        /// <see cref="Presenter{TView}"/>. May be an interface.
        /// </summary>
        Type ViewType { get; }

        /// <summary>
        /// Attaches (or, with <c>null</c>, detaches) the view.
        /// </summary>
        /// <param name="view">
        /// An instance assignable to <see cref="ViewType"/>, or <c>null</c> to detach.
        /// </param>
        /// <exception cref="InvalidCastException"><paramref name="view"/> is not assignable to
        /// <see cref="ViewType"/>. This is deliberate: a mistyped view is a wiring bug, and the cast names
        /// both types.</exception>
        void SetView(object view);
    }

    /// <summary>
    /// The non-generic face of a presenter that has a model. Implemented explicitly by
    /// <see cref="Presenter{TView,TModel}"/>.
    /// </summary>
    /// <remarks>
    /// <i>Changed in 2.0.0</i> — <c>ModelChanged</c>, <c>object Model</c>, <c>OnBeforeModelChange</c> and
    /// <c>OnAfterModelChanged</c> were removed, and this interface no longer derives from
    /// <see cref="IPresenterWithView"/>. <c>ModelChanged</c> existed solely to drive the deleted
    /// <c>OnReady</c> latch; the two hooks are the class's own <c>protected virtual</c> members; and a model
    /// does not imply a view.
    /// </remarks>
    public interface IPresenterWithModel
    {
        /// <summary>
        /// Replaces the model, then refreshes the view if one is attached and alive.
        /// </summary>
        /// <param name="model">The new model. May be <c>null</c> if <c>TModel</c> is a reference
        /// type.</param>
        /// <exception cref="InvalidCastException"><paramref name="model"/> is not assignable to the
        /// presenter's <c>TModel</c>.</exception>
        void SetModel(object model);
    }

    /// <summary>
    /// A presenter whose model is known statically. Exists to be used as a generic constraint — it is what
    /// lets an opening API shaped like <c>Open&lt;TPresenter, TModel&gt;(TModel model)</c> reject a model the
    /// presenter cannot hold, at compile time rather than with a cast at the far end of a callback.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public interface IPresenterWithModel<TModel> : IPresenterWithModel
    {
        /// <summary>
        /// The current model. <c>default</c> until <see cref="IPresenterWithModel.SetModel"/> is first
        /// called.
        /// </summary>
        TModel Model { get; }
    }

    /// <summary>
    /// A node in the presenter tree: a scoped, injected object with children, whose lifetime is a
    /// <see cref="OpenUGD.Lifetime"/> nested inside its parent's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three methods.</b> <see cref="AddPresenter{T}"/> grows the tree, <see cref="Close"/> ends a branch,
    /// <see cref="Dispose"/> is <see cref="Close"/> under another name so a presenter composes with
    /// <c>using</c>. Everything else is an extension method over <see cref="Children"/>,
    /// <see cref="Lifetime"/> and <see cref="Context"/>; see <c>PresenterExtensions</c>.
    /// </para>
    /// <para>
    /// <b>A presenter is not constructed into the tree.</b> It is constructed by you (or by
    /// <see cref="OpenUGD.Context"/>), then attached with <see cref="AddPresenter{T}"/>, which is what gives
    /// it its <see cref="Lifetime"/>, its <see cref="Context"/> and its injected fields. Nothing in this
    /// class is usable from a constructor — <see cref="Lifetime"/> and <see cref="Context"/> throw there,
    /// loudly, rather than handing back <c>null</c>. Use <see cref="OnInitialize"/>.
    /// </para>
    /// <para>
    /// <b>Teardown.</b> Terminating a presenter's lifetime runs, in order: whatever
    /// <see cref="OnInitialize"/> and the presenter's own code registered on the lifetime (LIFO, per
    /// <see cref="OpenUGD.Lifetime"/>), then <see cref="OnClose"/>, then every child, then the unlink from
    /// the parent. Children are terminated leaf-last-registered-first, and each child repeats the sequence. A
    /// child that throws while closing does not prevent its siblings from closing: failures surface together
    /// as an <see cref="AggregateException"/> from <see cref="Close"/>.
    /// </para>
    /// <para>
    /// <b>Engine-free.</b> Nothing in this file references <c>UnityEngine</c>. A presenter tree can be built,
    /// driven and asserted on in a plain unit test.
    /// </para>
    /// <para>
    /// <b>Breaking changes in 2.0.0.</b> <c>OnReady</c> is gone — see
    /// <see cref="Presenter{TView}.OnRefresh"/> for why and for what replaces it. <c>ISubscribeNotify</c>,
    /// <c>Presenter.Internal.Ready</c> and the per-presenter notification <c>Signal</c> that existed only to
    /// drive it are gone with it. The presenter no longer implements <c>IResolve</c>/<c>IInject</c> from the
    /// deprecated dependency-injection package; use <see cref="Context"/>. <c>Children</c> is a live view
    /// rather than a fresh array per call.
    /// </para>
    /// </remarks>
    public abstract class Presenter : IDisposable
    {
        private readonly List<Presenter> _children = new List<Presenter>();
        private Lifetime.Definition _definition;
        private Context _context;
        private bool _initialized;

        /// <summary>
        /// This presenter's scope. Terminates when the presenter closes, when its parent closes, or when the
        /// context that owns the tree is disposed.
        /// </summary>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
        public Lifetime Lifetime => _definition != null ? _definition.Lifetime : throw NotInitialized();

        /// <summary>
        /// The context this presenter was injected from, and the one its children will be injected from. Use
        /// it to resolve a service a presenter needs but does not want as a constructor parameter.
        /// </summary>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
        public Context Context => _context != null ? _context : throw NotInitialized();

        /// <summary>
        /// This presenter's children, in attachment order.
        /// </summary>
        /// <remarks>
        /// A <b>live view</b>, not a copy: it changes as children are attached and closed, so do not hold it
        /// across anything that can close a presenter, and do not iterate it while closing children. Use
        /// <c>PresenterExtensions.GetChildren</c> for a snapshot.
        /// </remarks>
        public IReadOnlyList<Presenter> Children => _children;

        /// <summary>
        /// The presenter this one is attached to, or <c>null</c> for a <see cref="Root"/> and after closing.
        /// </summary>
        protected Presenter Parent { get; private set; }

        /// <summary>
        /// Closes this presenter and, with it, its whole subtree. Idempotent.
        /// </summary>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
        /// <exception cref="AggregateException">One or more clean-up actions threw. Every one of them still
        /// ran, and the presenter is closed either way.</exception>
        public void Close() => (_definition ?? throw NotInitialized()).Terminate();

        /// <summary>
        /// Same as <see cref="Close"/>; lets a presenter be used with <c>using</c>.
        /// </summary>
        public void Dispose() => Close();

        /// <summary>
        /// Attaches <paramref name="presenter"/> as a child: gives it a nested
        /// <see cref="OpenUGD.Lifetime"/>, injects it from this presenter's <see cref="Context"/>, and calls
        /// its <see cref="OnInitialize"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The child is fully initialized when this returns, so the caller may immediately call
        /// <c>SetView</c> / <c>SetModel</c> on it. Those two are what drive rendering; attaching alone
        /// renders nothing.
        /// </para>
        /// <para>
        /// The child closes when this presenter closes. To close it earlier, call <see cref="Close"/> on it.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The concrete presenter type, returned unchanged so calls can be
        /// chained.</typeparam>
        /// <param name="presenter">The presenter to attach. Must be freshly constructed.</param>
        /// <returns><paramref name="presenter"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="presenter"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="presenter"/> is already a child of this
        /// presenter.</exception>
        /// <exception cref="InvalidOperationException">
        /// This presenter has not been attached yet, or its lifetime has already terminated, or
        /// <paramref name="presenter"/> has already been attached somewhere. A presenter is attached once;
        /// there is no reparenting, because its lifetime is its parent's.
        /// </exception>
        public T AddPresenter<T>(T presenter) where T : Presenter
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");
            if (!_initialized)
                throw NotInitialized();
            if (presenter._initialized)
                throw new InvalidOperationException(
                    $"{presenter.GetType().Name} has already been attached; a presenter cannot be attached twice");
            if (_children.Contains(presenter))
                throw new ArgumentException($"{presenter.GetType().Name} is already a child of {GetType().Name}",
                    nameof(presenter));

            // Defined first: this throws if our own lifetime is already terminated, so a failure cannot
            // leave a half-attached child in _children.
            var definition = Lifetime.DefineNested(presenter.GetType().Name);

            _children.Add(presenter);
            presenter.Parent = this;
            Internal.Initialize(_context, presenter, definition);

            return presenter;
        }

        /// <summary>
        /// Called once, after the presenter has been attached and injected, and before any view or model is
        /// set. The place to create signals and to register clean-up on <see cref="Lifetime"/>.
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Called once, when the presenter's lifetime terminates, before its children are closed.
        /// </summary>
        /// <remarks>
        /// Anything registered on <see cref="Lifetime"/> unwinds first, so prefer <c>Lifetime.AddAction</c>
        /// for releasing a specific resource and keep this for the presenter's own "I am going away" logic.
        /// </remarks>
        protected virtual void OnClose()
        {
        }

        private InvalidOperationException NotInitialized() => new InvalidOperationException(
            $"{GetType().Name} has not been attached yet, so it has no Lifetime and no Context. " +
            "A presenter receives both from Presenter.AddPresenter (or from Presenter.Root's constructor). " +
            "Do not touch them in a constructor - use OnInitialize.");

        /// <summary>
        /// The root of a presenter tree: the one presenter that is attached to a
        /// <see cref="OpenUGD.Lifetime"/> and a <see cref="OpenUGD.Context"/> directly instead of to a parent
        /// presenter.
        /// </summary>
        public sealed class Root : Presenter
        {
            /// <summary>
            /// Creates the root and initializes it immediately.
            /// </summary>
            /// <param name="lifetime">The lifetime the tree lives inside. A nested scope is defined on
            /// it.</param>
            /// <param name="context">The context every presenter in this tree is injected from.</param>
            /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
            /// <exception cref="InvalidOperationException"><paramref name="lifetime"/> has already
            /// terminated.</exception>
            public Root(Lifetime lifetime, Context context)
            {
                if (lifetime == null)
                    throw new ArgumentNullException(nameof(lifetime), $"{nameof(lifetime)} can't be null");
                if (context == null)
                    throw new ArgumentNullException(nameof(context), $"{nameof(context)} can't be null");

                Internal.Initialize(context, this, lifetime.DefineNested(nameof(Root)));
            }
        }

        /// <summary>
        /// Attachment, shared by <see cref="AddPresenter{T}"/> and <see cref="Root"/>. Not part of the public
        /// API.
        /// </summary>
        internal static class Internal
        {
            internal static void Initialize(Context context, Presenter presenter, Lifetime.Definition definition)
            {
                if (context == null)
                    throw new ArgumentNullException(nameof(context), $"{nameof(context)} can't be null");
                if (presenter == null)
                    throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");
                if (definition == null)
                    throw new ArgumentNullException(nameof(definition), $"{nameof(definition)} can't be null");
                if (presenter._initialized)
                    throw new InvalidOperationException(
                        $"{presenter.GetType().Name} has already been attached; a presenter cannot be attached twice");
                if (definition.IsTerminated)
                    throw new InvalidOperationException(
                        $"cannot attach {presenter.GetType().Name}: the lifetime it would be attached to has " +
                        "already terminated. Check IsTerminated before attaching, and skip the whole open.");

                presenter._context = context;
                presenter._definition = definition;
                presenter._initialized = true;

                definition.Lifetime.AddAction(() => {
                    presenter.OnClose();

                    if (presenter._children.Count != 0)
                    {
                        // Snapshot: each child removes itself from this list as it closes.
                        var children = presenter._children.ToArray();
                        for (var i = children.Length - 1; i >= 0; i--)
                        {
                            children[i]._definition.Terminate();
                        }
                    }

                    var parent = presenter.Parent;
                    if (parent != null)
                    {
                        parent._children.Remove(presenter);
                        presenter.Parent = null;
                    }
                });

                context.Inject(presenter);
                presenter.OnInitialize();
            }
        }
    }

    /// <summary>
    /// A presenter with a view: the thing that actually renders.
    /// </summary>
    /// <typeparam name="TView">
    /// The view type. Any reference type — a <c>MonoBehaviour</c>, an interface, or a plain class standing in
    /// for something the engine does not own, such as a native web overlay. There is deliberately no
    /// constraint: requiring the view type to answer for its own liveness cannot work for views this project
    /// does not own, <c>UnityEngine.UI.Button</c> being the obvious one.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// <b>Two hooks, split by responsibility rather than by time.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description> <see cref="OnViewAdded"/> runs <b>once per attached view</b>. Wiring: subscribe to
    /// buttons, cache child references, register clean-up. It must not depend on the model.
    /// </description></item> <item><description> <see cref="OnRefresh"/> runs <b>every time the presenter
    /// could look different</b> — after a view attaches and after every model change — and must be
    /// idempotent. Rendering, and only rendering. </description></item>
    /// </list>
    /// <para>
    /// <b>Why <c>OnReady</c> was deleted.</b> It was two mechanisms that disagreed. A child added through
    /// <see cref="Presenter.AddPresenter{T}"/> got a latch that waited for both a view and a model; a
    /// presenter opened by one of the 0.6.x UI services got a direct push that never looked at the model; and
    /// <see cref="Presenter.Root"/> got neither, so its <c>OnReady</c> never fired at all. Which of the three
    /// you got depended on how the presenter happened to be created — the exact shape of "keeps running while
    /// doing the wrong thing". The presenters showed what was actually wanted: one of them wrote the same
    /// render logic twice, in <c>OnAfterModelChanged</c> and again in <c>OnReady</c>, because neither hook
    /// alone guaranteed both halves; another set its sprite in <c>OnReady</c> and never updated it again.
    /// Both are one correct <see cref="OnRefresh"/>.
    /// </para>
    /// <para>
    /// <b><see cref="OnRefresh"/> only ever runs while this presenter is live</b> — a view is attached and
    /// the presenter's own scope has not ended — which is why a presenter body needs no <c>View != null</c>
    /// guard. It holds because whoever creates a view must tie the presenter's <see cref="Lifetime"/> to that
    /// view's destruction, so a live presenter always has a live view.
    /// </para>
    /// </remarks>
    public abstract class Presenter<TView> : Presenter, IPresenterWithView
        where TView : class
    {
        /// <summary>
        /// The attached view, or <c>null</c>. Inside <see cref="OnViewAdded"/> and <see cref="OnRefresh"/> it
        /// is never <c>null</c>.
        /// </summary>
        public TView View { get; private set; }

        /// <summary>
        /// True while this presenter's own scope is alive and a view is attached. <see cref="OnRefresh"/> is
        /// skipped when this is <c>false</c>, so a presenter body never needs its own guard.
        /// </summary>
        /// <remarks>
        /// The view's own liveness is deliberately NOT consulted. The invariant is that whoever creates a
        /// view ties this presenter's <see cref="Lifetime"/> to that view's destruction —
        /// <c>ViewBehaviour</c> terminates its scope in <c>OnDestroy</c>, and a borrowed scene view is
        /// bridged by <c>SignalMonoBehaviour.DestroySignal</c> — so a live presenter always has a live view.
        /// Asking the view type itself was tried and abandoned: it cannot work for views we do not own, such
        /// as <c>UnityEngine.UI.Button</c>.
        /// </remarks>
        protected bool IsLive => View != null && !Lifetime.IsTerminated;

        Type IPresenterWithView.ViewType => typeof(TView);

        void IPresenterWithView.SetView(object view) => SetView((TView)view);

        /// <summary>
        /// Attaches (or, with <c>null</c>, detaches) the view, then refreshes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting the view it already has does nothing. Otherwise, in order: the old view is detached and
        /// <see cref="OnViewAfterRemoved"/> runs if there was one; the new view is stored;
        /// <see cref="OnViewAdded"/> runs if it is non-<c>null</c>; then <see cref="Refresh"/>.
        /// </para>
        /// <para>
        /// <i>Changed in 2.0.0</i> — <c>OnViewAfterRemoved</c> no longer fires on the first attach, when
        /// there was no previous view to remove.
        /// </para>
        /// </remarks>
        /// <param name="view">The view to attach, or <c>null</c> to detach.</param>
        public void SetView(TView view)
        {
            if (ReferenceEquals(view, View)) return;

            if (View != null)
            {
                View = null;
                OnViewAfterRemoved();
            }

            View = view;

            if (view != null)
            {
                OnViewAdded();
            }

            Refresh();
        }

        /// <summary>
        /// Re-renders: calls <see cref="OnRefresh"/> if a view is attached and alive, and does nothing
        /// otherwise.
        /// </summary>
        /// <remarks>
        /// Call this when something the presenter renders from changed but the model object did not — a
        /// language switch, say. It is always safe to call.
        /// </remarks>
        public void Refresh()
        {
            if (IsLive)
            {
                OnRefresh();
            }
        }

        /// <summary>
        /// Called once each time a view is attached, with <see cref="View"/> already set. Wiring goes here:
        /// subscriptions, listeners, clean-up registered on <see cref="Presenter.Lifetime"/>.
        /// </summary>
        /// <remarks>
        /// Do not render here — <see cref="OnRefresh"/> runs immediately afterwards and again on every model
        /// change, so anything rendered here would be written twice and then go stale.
        /// </remarks>
        protected virtual void OnViewAdded()
        {
        }

        /// <summary>
        /// Called once each time a view is detached, with <see cref="View"/> already <c>null</c>. The view
        /// being replaced may already be destroyed, so release things the presenter owns, not things on the
        /// view.
        /// </summary>
        protected virtual void OnViewAfterRemoved()
        {
        }

        /// <summary>
        /// Renders the presenter from its current view and model. Called after a view attaches and after
        /// every model change; never called without a live view.
        /// </summary>
        /// <remarks>
        /// <b>Must be idempotent.</b> Running it twice in a row must leave the same result as running it
        /// once, because the number of times it runs is not part of the contract. Set values here; do not
        /// subscribe, allocate per-call scopes, or start animations that assume a fresh start.
        /// </remarks>
        protected virtual void OnRefresh()
        {
        }
    }

    /// <summary>
    /// A presenter with a view and a model.
    /// </summary>
    /// <typeparam name="TView">The view type; see <see cref="Presenter{TView}"/>.</typeparam>
    /// <typeparam name="TModel">The model type. Anything, including a value type.</typeparam>
    public abstract class Presenter<TView, TModel> : Presenter<TView>, IPresenterWithModel<TModel>
        where TView : class
    {
        /// <summary>
        /// The current model. <c>default(TModel)</c> until <see cref="SetModel"/> is first called.
        /// </summary>
        public TModel Model { get; private set; }

        void IPresenterWithModel.SetModel(object model) => SetModel((TModel)model);

        /// <summary>
        /// Replaces the model and re-renders.
        /// </summary>
        /// <remarks>
        /// In order: <see cref="OnBeforeModelChange"/> with the old model still in place, then
        /// <see cref="Model"/> is replaced, then <see cref="Presenter{TView}.Refresh"/>. Setting the same
        /// model again is not special-cased — it re-renders, which is exactly what you want when the model is
        /// mutable.
        /// </remarks>
        /// <param name="model">The new model.</param>
        public void SetModel(TModel model)
        {
            OnBeforeModelChange();
            Model = model;
            Refresh();
        }

        /// <summary>
        /// Called with <see cref="Model"/> still holding the outgoing model, immediately before it is
        /// replaced. For releasing something that was created from the old model.
        /// </summary>
        protected virtual void OnBeforeModelChange()
        {
        }

    }
}
