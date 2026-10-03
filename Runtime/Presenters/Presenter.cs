using System;
using System.Collections.Generic;

namespace OpenUGD.Presenters
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
        /// Attaches (or, with <c>null</c>, detaches) the view. Same as <see cref="Presenter{TView}.SetView"/>,
        /// including what it does before attach and after close.
        /// </summary>
        /// <param name="view">
        /// An instance assignable to <see cref="ViewType"/>, or <c>null</c> to detach.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="view"/> is not assignable to
        /// <see cref="ViewType"/>. The message names the presenter, its view type and the type it was given.
        /// <i>Changed in 2.0.0</i> — this was an <see cref="InvalidCastException"/> that named neither the
        /// presenter nor the parameter.</exception>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
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
        /// <param name="model">The new model. May be <c>null</c> if <c>TModel</c> is a reference type or a
        /// <see cref="Nullable{T}"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="model"/> is not assignable to the presenter's
        /// <c>TModel</c>, or is <c>null</c> and <c>TModel</c> is a non-nullable value type. The message names the
        /// presenter, its model type and what it was given. <i>Changed in 2.0.0</i> — this was an
        /// <see cref="InvalidCastException"/>, or a <see cref="NullReferenceException"/> for a <c>null</c> value
        /// type, that named neither the presenter nor the parameter.</exception>
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
    /// <c>using</c>. <see cref="Attach"/> is the one static entry point, for code that roots a presenter on a
    /// scope of its own. Everything else is an extension method over <see cref="Children"/> and
    /// <see cref="Lifetime"/>; see <c>PresenterExtensions</c>.
    /// </para>
    /// <para>
    /// <b>A presenter is not constructed into the tree.</b> It is constructed by you (or by an
    /// <see cref="IPresenterFactory"/>), then attached — by <see cref="AddPresenter{T}"/>, by
    /// <see cref="Attach"/> or, for the root, by <see cref="Root"/>'s constructor — which is what gives it its
    /// <see cref="Lifetime"/> and has its <see cref="IPresenterFactory"/> inject it. Nothing in this class is
    /// usable from a constructor — <see cref="Lifetime"/> throws there, loudly, rather than handing back
    /// <c>null</c>. Use <see cref="OnInitialize"/>.
    /// </para>
    /// <para>
    /// <b>No container.</b> This assembly references <c>com.openugd.lifetime</c> and nothing else from the
    /// family. Construction and injection go through the <see cref="IPresenterFactory"/> the tree was rooted
    /// with; <c>ContextPresenterFactory</c> in <c>com.openugd.corelib</c> is the one over
    /// <c>OpenUGD.Context</c>, and with it <c>[Inject]</c> members of a presenter attached with <c>new</c> are
    /// filled in before <see cref="OnInitialize"/>. A presenter reaches a service through such a member, not
    /// through the container: there is no <c>Context</c> property.
    /// </para>
    /// <para>
    /// <b>Teardown.</b> Terminating a presenter's lifetime unwinds it in reverse order of registration, per
    /// <see cref="OpenUGD.Lifetime"/>: children attached later, clean-up registered on <see cref="Lifetime"/>
    /// (including a view's <c>ViewLifetime</c>), and children attached earlier, all newest first; then
    /// <see cref="OnClose"/>; then the unlink from the parent. So a presenter's children have closed by the time
    /// its <see cref="OnClose"/> runs, and each child repeats the sequence. A child that throws while closing
    /// does not prevent its siblings from closing. The failures are reported by <see cref="Close"/> once
    /// everything has run, as <see cref="OpenUGD.Lifetime"/> reports them: a single failure as itself, two or
    /// more as one <see cref="AggregateException"/>.
    /// </para>
    /// <para>
    /// <b>Engine-free.</b> Nothing in this assembly references <c>UnityEngine</c>. A presenter tree can be
    /// built, driven and asserted on in a plain unit test, with a hand-written <see cref="IPresenterFactory"/>.
    /// </para>
    /// <para>
    /// <b>Breaking changes in 2.0.0.</b> <c>OnReady</c> is gone — see
    /// <see cref="Presenter{TView}.OnRefresh"/> for why and for what replaces it. <c>ISubscribeNotify</c>,
    /// <c>Presenter.Internal.Ready</c> and the per-presenter notification <c>Signal</c> that existed only to
    /// drive it are gone with it. The presenter no longer implements <c>IResolve</c>/<c>IInject</c> from the
    /// deprecated dependency-injection package, and does not expose a container either: use an injected member.
    /// <c>Children</c> is a live view rather than a fresh array per call.
    /// </para>
    /// </remarks>
    public abstract class Presenter : IDisposable
    {
        private readonly List<Presenter> _children = new List<Presenter>();
        private Lifetime.Definition _definition;
        private IPresenterFactory _factory;

        /// <summary>
        /// This presenter's scope. Terminates when the presenter closes, when its parent closes, or when the
        /// scope it was attached under terminates.
        /// </summary>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
        public Lifetime Lifetime => _definition != null ? _definition.Lifetime : throw NotAttached();

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
        /// The presenter this one is attached to, or <c>null</c> for a <see cref="Root"/>, for a presenter
        /// attached with <see cref="Attach"/>, and after closing.
        /// </summary>
        protected Presenter Parent { get; private set; }

        /// <summary>
        /// Whether <see cref="Attach"/> has run for this presenter. Stays <c>true</c> after it closes.
        /// </summary>
        private protected bool IsAttached => _definition != null;

        /// <summary>
        /// Closes this presenter and, with it, its whole subtree. Idempotent.
        /// </summary>
        /// <remarks>
        /// If clean-up throws — <see cref="OnClose"/>, an action registered on <see cref="Lifetime"/>, or a
        /// child's — the rest of the subtree still closes and this presenter is closed either way; the
        /// failures are reported once the last clean-up has run.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
        /// <exception cref="Exception">Exactly one clean-up action threw: that exception, rethrown with its
        /// original stack trace.</exception>
        /// <exception cref="AggregateException">Two or more clean-up actions threw.</exception>
        public void Close() => (_definition ?? throw NotAttached()).Terminate();

        /// <summary>
        /// Same as <see cref="Close"/>; lets a presenter be used with <c>using</c>.
        /// </summary>
        public void Dispose() => Close();

        /// <summary>
        /// Attaches <paramref name="presenter"/> as a child: gives it a <see cref="OpenUGD.Lifetime"/> nested in
        /// this presenter's, has this presenter's <see cref="IPresenterFactory"/> inject it, and calls its
        /// <see cref="OnInitialize"/>.
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
        /// <exception cref="InvalidOperationException">
        /// This presenter has not been attached yet, or its lifetime has already terminated, or
        /// <paramref name="presenter"/> has already been attached somewhere. A presenter is attached once;
        /// there is no reparenting, because its lifetime is its parent's.
        /// </exception>
        public T AddPresenter<T>(T presenter) where T : Presenter
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");
            if (_definition == null)
                throw NotAttached();
            if (presenter._definition != null)
                throw AlreadyAttached(presenter);

            // Defined and checked before the child is linked, so attaching to a closed parent cannot leave a
            // half-attached child in _children. A scope defined on a terminated lifetime is born terminated
            // rather than throwing.
            var definition = Lifetime.DefineNested(presenter.GetType().Name);
            if (definition.IsTerminated)
                throw new InvalidOperationException(
                    $"cannot attach {presenter.GetType().Name} to {GetType().Name}: {GetType().Name} has " +
                    "already closed. Check Lifetime.IsTerminated before attaching, and skip the whole open.");

            _children.Add(presenter);
            presenter.Parent = this;
            Attach(presenter, definition, _factory);

            return presenter;
        }

        /// <summary>
        /// Attaches <paramref name="presenter"/> to a scope of the caller's: gives it
        /// <paramref name="definition"/> as its lifetime, has <paramref name="factory"/> inject it, and calls its
        /// <see cref="OnInitialize"/>. The presenter then belongs to no parent; its children are injected by
        /// <paramref name="factory"/> too.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what <see cref="AddPresenter{T}"/> and <see cref="Root"/> do underneath, made public for code
        /// that owns the scope a presenter lives in — a presenter-opening service, typically, which defines one
        /// scope per open and attaches the presenter it built to it. The usual sequence is <c>Attach</c>, then
        /// <c>SetModel</c>, then <c>SetView</c>, which renders once with both in place.
        /// </para>
        /// <para>
        /// <b>Ownership.</b> The presenter takes <paramref name="definition"/> over: <see cref="Close"/>
        /// terminates it, and terminating it closes the presenter. In that order, within the attach: the
        /// presenter's teardown is registered on the lifetime, <paramref name="factory"/>'s
        /// <see cref="IPresenterFactory.Inject"/> runs, then <see cref="OnInitialize"/>. An exception from
        /// either of the last two propagates to the caller.
        /// </para>
        /// </remarks>
        /// <param name="presenter">The presenter to attach. Must not have been attached before.</param>
        /// <param name="definition">The scope to give it. Must still be alive: a host must check
        /// <see cref="OpenUGD.Lifetime.Definition.IsTerminated"/> first and skip the whole open if it has
        /// ended.</param>
        /// <param name="factory">Injects <paramref name="presenter"/> and every child later attached under
        /// it.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="presenter"/> has already been attached,
        /// or <paramref name="definition"/> has already terminated.</exception>
        public static void Attach(Presenter presenter, Lifetime.Definition definition, IPresenterFactory factory)
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");
            if (definition == null)
                throw new ArgumentNullException(nameof(definition), $"{nameof(definition)} can't be null");
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), $"{nameof(factory)} can't be null");
            if (presenter._definition != null)
                throw AlreadyAttached(presenter);
            if (definition.IsTerminated)
                throw new InvalidOperationException(
                    $"cannot attach {presenter.GetType().Name}: the lifetime it would be attached to has " +
                    "already terminated. Check IsTerminated before attaching, and skip the whole open.");

            presenter._factory = factory;
            presenter._definition = definition;
            definition.Lifetime.AddAction(presenter.Teardown);

            factory.Inject(presenter);
            presenter.OnInitialize();
        }

        /// <summary>
        /// Called once, after the presenter has been attached and injected, and before any view or model is
        /// set. The place to create signals and to register clean-up on <see cref="Lifetime"/>.
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Called once, when the presenter's lifetime terminates, after its children have closed and after the
        /// clean-up registered on <see cref="Lifetime"/> has run.
        /// </summary>
        /// <remarks>
        /// Anything registered on <see cref="Lifetime"/> unwinds first, so prefer <c>Lifetime.AddAction</c>
        /// for releasing a specific resource and keep this for the presenter's own "I am going away" logic.
        /// </remarks>
        protected virtual void OnClose()
        {
        }

        // Registered on the presenter's lifetime at attach, so it is the oldest entry and runs last: everything
        // nested in the lifetime afterwards — children, the view scope, the presenter's own clean-up — has
        // unwound by the time it runs.
        private void Teardown()
        {
            OnClose();

            if (_children.Count != 0)
            {
                // Snapshot: each child removes itself from this list as it closes.
                var children = _children.ToArray();
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    children[i]._definition.Terminate();
                }
            }

            var parent = Parent;
            if (parent != null)
            {
                parent._children.Remove(this);
                Parent = null;
            }
        }

        private InvalidOperationException NotAttached() => new InvalidOperationException(
            $"{GetType().Name} has not been attached yet, so it has no Lifetime. A presenter is attached by " +
            "Presenter.AddPresenter, Presenter.Attach or Presenter.Root's constructor. Do not touch it in a " +
            "constructor - use OnInitialize.");

        private static InvalidOperationException AlreadyAttached(Presenter presenter) =>
            new InvalidOperationException(
                $"{presenter.GetType().Name} has already been attached; a presenter cannot be attached twice");

        /// <summary>
        /// The root of a presenter tree: a presenter with no view, attached to a scope nested in a
        /// <see cref="OpenUGD.Lifetime"/> instead of to a parent presenter, to hold the presenters added under it.
        /// </summary>
        public sealed class Root : Presenter
        {
            /// <summary>
            /// Creates the root and attaches it immediately, as <see cref="Presenter.Attach"/> does.
            /// </summary>
            /// <param name="lifetime">The lifetime the tree lives inside. A nested scope is defined on
            /// it.</param>
            /// <param name="factory">Injects every presenter attached in this tree.</param>
            /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
            /// <exception cref="InvalidOperationException"><paramref name="lifetime"/> has already
            /// terminated.</exception>
            public Root(Lifetime lifetime, IPresenterFactory factory)
            {
                if (lifetime == null)
                    throw new ArgumentNullException(nameof(lifetime), $"{nameof(lifetime)} can't be null");
                if (factory == null)
                    throw new ArgumentNullException(nameof(factory), $"{nameof(factory)} can't be null");

                Attach(this, lifetime.DefineNested(nameof(Root)), factory);
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
    /// the view's events, cache child references, and register the matching clean-up on
    /// <see cref="ViewLifetime"/>. It must not depend on the model. </description></item>
    /// <item><description> <see cref="OnRefresh"/> runs <b>every time the presenter could look different</b> —
    /// after a view attaches and after every model change — and must be idempotent. Rendering, and only
    /// rendering. </description></item>
    /// </list>
    /// <para>
    /// <b>Two scopes.</b> <see cref="Presenter.Lifetime"/> is the presenter's; <see cref="ViewLifetime"/> is the
    /// current view's, nested in it. A listener added to a view in <see cref="OnViewAdded"/> belongs to the view
    /// scope: registered on <see cref="Presenter.Lifetime"/> instead, it would outlive the view when the view is
    /// replaced or detached, and a re-attached view would be subscribed twice.
    /// </para>
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
    /// <b><see cref="OnRefresh"/> only ever runs while this presenter is live</b> — see <see cref="IsLive"/> —
    /// which is why a presenter body needs no <c>View != null</c> guard.
    /// </para>
    /// <para>
    /// <b>Attach first.</b> <see cref="SetView"/> throws before the presenter is attached, and does nothing once
    /// it has closed. <see cref="Presenter{TView,TModel}.SetModel"/> may be called at any time; it renders only
    /// while the presenter is live.
    /// </para>
    /// </remarks>
    public abstract class Presenter<TView> : Presenter, IPresenterWithView
        where TView : class
    {
        private Lifetime.Definition _viewDefinition;

        /// <summary>
        /// The attached view, or <c>null</c>. Inside <see cref="OnViewAdded"/> and <see cref="OnRefresh"/> it
        /// is never <c>null</c>.
        /// </summary>
        public TView View { get; private set; }

        /// <summary>
        /// The scope of the current view: defined just before <see cref="OnViewAdded"/> and terminated just
        /// before that view is detached or replaced, while <see cref="View"/> still holds it. The place to
        /// register the clean-up of whatever <see cref="OnViewAdded"/> wired to the view.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Nested in <see cref="Presenter.Lifetime"/>, so it also ends when the presenter closes, before
        /// <see cref="Presenter.OnClose"/>. Each attached view gets a fresh one, so a view that is detached and
        /// attached again is wired exactly once each time.
        /// </para>
        /// <code>
        /// protected override void OnViewAdded()
        /// {
        ///     var button = View;
        ///     button.onClick.AddListener(OnClick);
        ///     ViewLifetime.AddAction(() => button.onClick.RemoveListener(OnClick));
        /// }
        /// </code>
        /// </remarks>
        /// <exception cref="InvalidOperationException">No view has been attached yet, or the view was
        /// detached.</exception>
        protected Lifetime ViewLifetime =>
            _viewDefinition != null
                ? _viewDefinition.Lifetime
                : throw new InvalidOperationException(
                    $"{GetType().Name} has no view attached, so it has no ViewLifetime. It exists from just " +
                    "before OnViewAdded until the view is detached or replaced: read it in OnViewAdded or " +
                    "OnRefresh.");

        /// <summary>
        /// True while a view is attached, this presenter's own scope is alive, and the view has not been
        /// destroyed. <see cref="OnRefresh"/> is skipped when this is <c>false</c>, so a presenter body never
        /// needs its own guard.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The scope is the contract.</b> Whoever creates a view ties this presenter's
        /// <see cref="Presenter.Lifetime"/> to that view's destruction — by closing the presenter when the view's
        /// scope ends, or by attaching it to a scope that ends with the view — so a live presenter normally has a
        /// live view.
        /// </para>
        /// <para>
        /// <b>The view check is the backstop</b>, for a view whose destruction was not tied: a scene object
        /// destroyed by a scene unload, say. It is <c>!View.Equals(null)</c>, which is Unity's own test:
        /// <c>UnityEngine.Object</c> overrides <c>Equals</c> so that a destroyed object equals <c>null</c>, the
        /// same comparison its <c>==</c> operator and its <c>bool</c> conversion make. That is how this assembly
        /// asks Unity whether a view is alive without referencing <c>UnityEngine</c>, and why it works for views
        /// whose types are not ours, such as <c>UnityEngine.UI.Button</c>. For any other view type,
        /// <c>Equals(null)</c> is <c>false</c> by the .NET contract, so only the first two conditions apply.
        /// </para>
        /// <para>
        /// <i>Changed in 2.0.0</i> — this was <c>View != null</c>, a reference comparison that took a destroyed
        /// Unity view for a live one (audit UH-12).
        /// </para>
        /// </remarks>
        protected bool IsLive => View != null && !Lifetime.IsTerminated && !View.Equals(null);

        Type IPresenterWithView.ViewType => typeof(TView);

        void IPresenterWithView.SetView(object view)
        {
            if (view != null && !(view is TView))
                throw new ArgumentException(TypeNames.Rejected(this, "view", typeof(TView), view), nameof(view));

            SetView((TView)view);
        }

        /// <summary>
        /// Attaches (or, with <c>null</c>, detaches) the view, then refreshes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting the view it already has does nothing. Otherwise, in order: if there was a view,
        /// <see cref="ViewLifetime"/> terminates (with <see cref="View"/> still set), the view is detached and
        /// <see cref="OnViewAfterRemoved"/> runs; the new view is stored; if it is non-<c>null</c>, a fresh
        /// <see cref="ViewLifetime"/> is defined and <see cref="OnViewAdded"/> runs; then <see cref="Refresh"/>.
        /// </para>
        /// <para>
        /// If clean-up registered on the old <see cref="ViewLifetime"/> throws, the old view is still detached,
        /// and the exception propagates before <see cref="OnViewAfterRemoved"/> and before the new view is
        /// stored.
        /// </para>
        /// <para>
        /// <b>Before attach</b> it throws, before touching anything: a presenter has no scope to wire a view in
        /// until it is attached. <b>Once the presenter has closed</b> — from the moment its lifetime starts
        /// terminating — it does nothing: the view is not stored, no hook runs, and the caller keeps ownership of
        /// the view. That makes a late view harmless, which is the ordinary case for a host whose view finished
        /// loading after the presenter was closed; such a host releases the view through a clean-up registered on
        /// <see cref="Presenter.Lifetime"/>, which runs at once on a closed presenter.
        /// </para>
        /// <para>
        /// <i>Changed in 2.0.0</i> — <c>OnViewAfterRemoved</c> no longer fires on the first attach, when
        /// there was no previous view to remove. A view set before attach used to be half-applied, and every
        /// later <see cref="Refresh"/> threw (audit CC-10).
        /// </para>
        /// </remarks>
        /// <param name="view">The view to attach, or <c>null</c> to detach.</param>
        /// <exception cref="InvalidOperationException">The presenter has not been attached yet.</exception>
        public void SetView(TView view)
        {
            if (!IsAttached)
                throw new InvalidOperationException(
                    $"{GetType().Name}.SetView was called before the presenter was attached. Attach it first - " +
                    "Presenter.AddPresenter, Presenter.Attach - then set the model and the view.");
            if (Lifetime.IsTerminated) return;
            if (ReferenceEquals(view, View)) return;

            if (View != null)
            {
                var definition = _viewDefinition;
                _viewDefinition = null;
                try
                {
                    definition?.Terminate();
                }
                finally
                {
                    View = null;
                }

                OnViewAfterRemoved();
            }

            View = view;

            if (view != null)
            {
                _viewDefinition = Lifetime.DefineNested(nameof(View));
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
        /// Called once each time a view is attached, with <see cref="View"/> and a fresh
        /// <see cref="ViewLifetime"/> already set. Wiring goes here: listeners and subscriptions on the view,
        /// each with its clean-up registered on <see cref="ViewLifetime"/>.
        /// </summary>
        /// <remarks>
        /// Do not render here — <see cref="OnRefresh"/> runs immediately afterwards and again on every model
        /// change, so anything rendered here would be written twice and then go stale.
        /// </remarks>
        protected virtual void OnViewAdded()
        {
        }

        /// <summary>
        /// Called once each time a view is detached, with <see cref="View"/> already <c>null</c> and the view's
        /// <see cref="ViewLifetime"/> already terminated. The view being replaced may already be destroyed, so
        /// release things the presenter owns, not things on the view.
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

        void IPresenterWithModel.SetModel(object model)
        {
            if (model is TModel typed)
            {
                SetModel(typed);
                return;
            }

            // null is a TModel only when TModel can hold it: a reference type or a Nullable<T>.
            if (model == null && (!typeof(TModel).IsValueType || Nullable.GetUnderlyingType(typeof(TModel)) != null))
            {
                SetModel(default);
                return;
            }

            throw new ArgumentException(TypeNames.Rejected(this, "model", typeof(TModel), model), nameof(model));
        }

        /// <summary>
        /// Replaces the model and re-renders.
        /// </summary>
        /// <remarks>
        /// In order: <see cref="OnBeforeModelChange"/> with the old model still in place, then
        /// <see cref="Model"/> is replaced, then <see cref="Presenter{TView}.Refresh"/>. Setting the same
        /// model again is not special-cased — it re-renders, which is exactly what you want when the model is
        /// mutable. It may be called at any time, before attach and after close included; it renders only while
        /// the presenter is live.
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

    // The messages of the object-typed SetView/SetModel: name the presenter, what it expects and what it got, in
    // the C# spelling of the types rather than the CLR's (List<int>, not List`1[[System.Int32, ...]]).
    internal static class TypeNames
    {
        internal static string Rejected(Presenter presenter, string what, Type expected, object actual)
        {
            var given = actual == null ? "a null " + what : $"a {what} of type {Of(actual.GetType())}";
            var nonNull = actual == null ? ", which cannot be null" : "";
            return $"{Of(presenter.GetType())} cannot take {given}: its {what} type is {Of(expected)}{nonNull}.";
        }

        internal static string Of(Type type)
        {
            if (type.IsArray)
                return Of(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericParameter)
                return type.Name;

            var prefix = type.IsNested
                ? Of(type.DeclaringType) + "."
                : string.IsNullOrEmpty(type.Namespace) ? "" : type.Namespace + ".";
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick < 0)
                return prefix + name;

            // A nested type's generic arguments start with its declaring type's; its own are the last n.
            var own = int.Parse(name.Substring(tick + 1));
            var arguments = type.GetGenericArguments();
            var spelled = new string[own];
            for (var i = 0; i < own; i++)
            {
                spelled[i] = Of(arguments[arguments.Length - own + i]);
            }

            return prefix + name.Substring(0, tick) + "<" + string.Join(", ", spelled) + ">";
        }
    }
}
