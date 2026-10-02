using System;
using UnityEngine;
#if OPENUGD_UGUI_PACKAGE
using UnityEngine.EventSystems;
#endif

namespace OpenUGD.Core
{
    /// <summary>
    /// One game instance bound to one display: the event system that reads its input, the audio listener
    /// that hears it, and a selected flag that switches both on and off together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sample code, yours to change.</b> This is the corelib 0.6.x component, moved out of the package in
    /// 2.0.0. The script keeps its original GUID (<c>bde234200e4d407696b34ff23b9e6ea2</c>), its namespace and
    /// its serialized field names, so a prefab that carried the 0.6.x component binds to this copy with no
    /// "Missing script".
    /// </para>
    /// <para>
    /// <b>Focus is enable/disable, not create/destroy.</b> Unity supports one enabled <c>AudioListener</c>
    /// and one enabled <c>EventSystem</c> at a time, so several instances can coexist in one process only
    /// if all but one are switched off. That is the whole of what <see cref="Select"/> and
    /// <see cref="Unselect"/> do, which makes switching cheap enough to drive from a controller-assignment
    /// screen.
    /// </para>
    /// <para>
    /// <b>Optional modules.</b> The event system exists only when the project has <c>com.unity.ugui</c>, and
    /// the audio listener only when it has the built-in Audio module (<c>com.unity.modules.audio</c>). The
    /// sample's assembly definition defines <c>OPENUGD_UGUI_PACKAGE</c> and <c>OPENUGD_AUDIO_MODULE</c> from
    /// those packages; without one, its property and its half of the focus switch are compiled out, and
    /// whatever the prefab serialized for it is ignored.
    /// </para>
    /// <para>
    /// <b>Nothing here enforces that only one instance is selected.</b> Calling <see cref="Select"/> on two
    /// components leaves two enabled event systems and two enabled listeners — which Unity does not
    /// support. Switch through <see cref="ContextFactoryInstancesComponent.Select"/>, which unselects the
    /// outgoing instance first.
    /// </para>
    /// <para>
    /// <b>Scope.</b> <c>Awake</c> creates a <see cref="Lifetime.Definition"/> nested in
    /// <see cref="Lifetime.Eternal"/>, and <c>OnDestroy</c> terminates it. Every subscription taken through
    /// <see cref="Subscribe"/> is therefore dropped when the <c>GameObject</c> is destroyed, even one whose
    /// own lifetime is still alive — the signal releases its handler references there, so a destroyed
    /// instance stops holding on to listeners that outlive it. The 0.6.x component created the definition in
    /// a field initializer, so an instance that was never activated — and so never received
    /// <c>OnDestroy</c> — left it on <see cref="Lifetime.Eternal"/> for the rest of the process.
    /// </para>
    /// <para>
    /// <b>The serialized references are required.</b> <see cref="Select"/> and <see cref="Unselect"/>
    /// dereference them unconditionally, so a prefab that left one empty fails on the first focus change
    /// (<see cref="UnassignedReferenceException"/> in the editor, <see cref="NullReferenceException"/> in a
    /// player) rather than degrading quietly.
    /// </para>
    /// </remarks>
    public class ContextInstanceComponent : MonoBehaviour, IContextInstanceProvider
    {
        private Lifetime.Definition _definition;
        private Signal _onChange;

#if OPENUGD_AUDIO_MODULE
        /// <summary>
        /// The audio listener that hears this instance's scene. Assigned in the inspector; required.
        /// </summary>
        /// <remarks>
        /// Its <c>enabled</c> flag is owned by <see cref="Select"/> and <see cref="Unselect"/> — set it
        /// yourself and the next focus change silently overwrites you. Compiled only when the project has
        /// the built-in Audio module.
        /// </remarks>
        [field: SerializeField] public AudioListener AudioListener { get; private set; }
#endif

#if OPENUGD_UGUI_PACKAGE
        /// <summary>
        /// The event system that serves this instance's UI. Assigned in the inspector; required.
        /// </summary>
        /// <remarks>
        /// Its <c>enabled</c> flag is owned by <see cref="Select"/> and <see cref="Unselect"/>, the same way
        /// the audio listener's is. Give each instance its own event system: sharing one between two
        /// instances defeats the point, since the flag is the only thing separating their input. Compiled
        /// only when the project has <c>com.unity.ugui</c>.
        /// </remarks>
        [field: SerializeField] public EventSystem EventSystem { get; private set; }
#endif

        /// <summary>
        /// The zero-based display index this instance renders to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why it is settable and hidden.</b> <see cref="ContextFactoryInstancesComponent"/> writes this
        /// onto the <i>prefab</i> immediately before each <c>Instantiate</c>, so every clone is born with
        /// its own index already serialized, then restores the prefab to <c>0</c>. Editing it by hand in
        /// the inspector would be overwritten on the next rebuild, which is why it is hidden there.
        /// </para>
        /// <para>
        /// Nothing in this component reads it. No camera, canvas or raycaster is retargeted for you — that
        /// is the game's job (<see cref="InstanceContextExample"/> shows it for cameras), and this property
        /// is only how the instance tells the game which display it is.
        /// </para>
        /// </remarks>
        [field: SerializeField]
        [field: HideInInspector]
        public int TargetDisplay { get; set; }

        /// <inheritdoc/>
        public bool IsSelected { get; private set; }

        private void Awake() => _definition = Lifetime.Eternal.DefineNested(nameof(ContextInstanceComponent));

        private void OnDestroy() => _definition?.Terminate();

        /// <summary>
        /// Registers <paramref name="listener"/> to run whenever <see cref="IsSelected"/> changes, for as
        /// long as <paramref name="lifetime"/> is alive.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The backing <see cref="Signal"/> is created on the first call, so an instance nobody watches
        /// never allocates one. It is scoped to this component, meaning the subscription ends at whichever
        /// comes first: <paramref name="lifetime"/> terminating, or the <c>GameObject</c> being destroyed.
        /// </para>
        /// <para>
        /// <b>Subscribing after the component has been destroyed is a silent no-op</b>, not an error — the
        /// signal is inert by then and nothing is registered. Same for an already-terminated
        /// <paramref name="lifetime"/>. Both cases are ordinary during a teardown or a rebuild, so neither
        /// throws. Subscribing <i>before</i> <c>Awake</c> does throw: an instance whose <c>GameObject</c>
        /// has never been active has no scope to hold the subscription.
        /// </para>
        /// <para>
        /// Nothing is invoked on subscription and nothing is handed to the listener; read
        /// <see cref="IsSelected"/> inside it. Listeners run in subscription order, and only on a real
        /// transition — see <see cref="Select"/>.
        /// </para>
        /// </remarks>
        /// <param name="lifetime">The <i>subscriber's</i> scope, not this component's.</param>
        /// <param name="listener">Invoked after the flag has already changed.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="lifetime"/> or <paramref name="listener"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Called before <c>Awake</c> — on an instance whose <c>GameObject</c> has never been active.
        /// </exception>
        public void Subscribe(Lifetime lifetime, Action listener)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    name + ": ContextInstanceComponent.Subscribe was called before Awake, so there is no scope " +
                    "to hold the subscription. Activate the GameObject first, or subscribe from Start.");
            }

            if (_onChange == null)
            {
                _onChange = new Signal(_definition.Lifetime);
            }

            _onChange.Subscribe(lifetime, listener);
        }

        /// <summary>
        /// Gives this instance input and audio focus: enables both the event system and the audio listener,
        /// then notifies subscribers — but only if this actually changed anything.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Selecting an already-selected instance re-asserts both enabled flags and notifies nobody.</b>
        /// That is deliberate and useful: it repairs the state if something else disabled a component
        /// behind your back, without waking every listener.
        /// </para>
        /// <para>
        /// <b>It does not unselect anyone else.</b> This method knows about one instance. Use
        /// <see cref="ContextFactoryInstancesComponent.Select"/> to move focus between instances.
        /// </para>
        /// </remarks>
        /// <exception cref="Exception">
        /// Exactly one subscriber threw: that exception, rethrown with its original stack trace. Every
        /// subscriber still ran, and this instance is selected either way — the exception is reported after
        /// the state change, never instead of it.
        /// </exception>
        /// <exception cref="AggregateException">
        /// Two or more subscribers threw. The same holds: every subscriber ran, and the instance is selected.
        /// </exception>
        public void Select()
        {
            var changed = !IsSelected;
            IsSelected = true;
            SetFocus(true);
            if (changed)
            {
                Fire();
            }
        }

        /// <summary>
        /// Takes input and audio focus away: disables both the event system and the audio listener, then
        /// notifies subscribers if this actually changed anything.
        /// </summary>
        /// <remarks>
        /// The mirror of <see cref="Select"/>, with the same "re-assert, do not re-notify" rule. Note that
        /// the first call on a freshly created instance disables both components but notifies nobody,
        /// because <see cref="IsSelected"/> was already <c>false</c> — which is exactly how
        /// <see cref="ContextFactoryInstancesComponent"/> silences the instances it creates after the
        /// first.
        /// </remarks>
        /// <exception cref="Exception">
        /// Exactly one subscriber threw: that exception, rethrown with its original stack trace. Every
        /// subscriber still ran, and this instance is unselected either way.
        /// </exception>
        /// <exception cref="AggregateException">
        /// Two or more subscribers threw. Every subscriber still ran, and this instance is unselected either
        /// way.
        /// </exception>
        public void Unselect()
        {
            var changed = IsSelected;
            IsSelected = false;
            SetFocus(false);
            if (changed)
            {
                Fire();
            }
        }

        private void SetFocus(bool on)
        {
#if OPENUGD_UGUI_PACKAGE
            EventSystem.enabled = on;
#endif
#if OPENUGD_AUDIO_MODULE
            AudioListener.enabled = on;
#endif
        }

        private void Fire()
        {
            if (_onChange != null)
            {
                _onChange.Fire();
            }
        }
    }
}
