using System;
#if OPENUGD_UGUI_PACKAGE
using UnityEngine.EventSystems;
#endif

namespace OpenUGD.Core
{
    /// <summary>
    /// The read-only face of one game instance pinned to one display: which display it draws to, whether it
    /// currently holds focus, and which <c>EventSystem</c> reads its input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sample code, yours to change.</b> This is the corelib 0.6.x interface, moved out of the package in
    /// 2.0.0 with its GUID and namespace, so code that consumed it compiles against this copy unchanged.
    /// </para>
    /// <para>
    /// <b>Read-only on purpose.</b> <see cref="ContextInstanceComponent"/> implements this and also exposes
    /// <c>Select</c> and <c>Unselect</c>; neither appears here. A consumer observes focus, it does not grant
    /// itself focus — switching is <see cref="ContextFactoryInstancesComponent.Select"/>'s job, because
    /// only the factory knows which instance has to be unselected first, and calling <c>Select</c>
    /// directly leaves two instances enabled at once.
    /// </para>
    /// <para>
    /// <b>Why an interface at all.</b> The implementation is a <c>MonoBehaviour</c>. Registering it under
    /// this contract — <c>builder.Services.AddInstance&lt;IContextInstanceProvider&gt;(component)</c>, one
    /// registration per instance context, as <see cref="InstanceContextExample"/> does — is what keeps
    /// everything downstream free of a scene reference.
    /// </para>
    /// </remarks>
    public interface IContextInstanceProvider
    {
#if OPENUGD_UGUI_PACKAGE
        /// <summary>
        /// The event system that serves this instance. Enabled only while <see cref="IsSelected"/> is
        /// <c>true</c>. Exists only when the project has <c>com.unity.ugui</c>.
        /// </summary>
        /// <remarks>
        /// Do not toggle <c>enabled</c> on it yourself: the next select or unselect overwrites the flag
        /// without consulting you. Change focus instead, and this follows.
        /// </remarks>
        EventSystem EventSystem { get; }
#endif

        /// <summary>
        /// The zero-based Unity display index this instance renders to, matching its position in the
        /// factory's instance list.
        /// </summary>
        /// <remarks>
        /// Assigned when the instance is created, and never rewritten afterwards by anything in this sample.
        /// Nothing acts on it either — no camera, canvas or raycaster is retargeted for you. Read it and point
        /// your own scene objects at <c>UnityEngine.Display</c>.
        /// </remarks>
        int TargetDisplay { get; }

        /// <summary>
        /// Whether this instance currently holds input and audio focus. Exactly one instance is expected to
        /// be selected at a time; nothing in this interface enforces that.
        /// </summary>
        /// <remarks>
        /// <c>false</c> on a freshly created instance until the factory selects or unselects it, and
        /// <c>false</c> on every instance for the brief window inside a focus switch, between the outgoing
        /// instance being unselected and the incoming one being selected.
        /// </remarks>
        bool IsSelected { get; }

        /// <summary>
        /// Registers <paramref name="listener"/> to run whenever <see cref="IsSelected"/> changes, until
        /// <paramref name="lifetime"/> terminates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Only real transitions are reported.</b> Selecting an already-selected instance re-asserts the
        /// underlying enabled flags but notifies nobody, so a listener cannot be used to count calls.
        /// </para>
        /// <para>
        /// Nothing is invoked on subscription and nothing is passed to the listener — read
        /// <see cref="IsSelected"/> inside it. There is no unsubscribe; terminate a nested
        /// <c>Lifetime.Definition</c> to detach early. The subscription also ends if the provider itself
        /// goes away, whatever <paramref name="lifetime"/> is still doing.
        /// </para>
        /// </remarks>
        /// <param name="lifetime">The <i>subscriber's</i> scope, not the provider's.</param>
        /// <param name="listener">Invoked after the change, with the new state already visible.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The provider is a component that has not run <c>Awake</c> yet.
        /// </exception>
        void Subscribe(Lifetime lifetime, Action listener);
    }
}
