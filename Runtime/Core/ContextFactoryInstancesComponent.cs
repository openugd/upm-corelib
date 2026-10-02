using System.Collections.Generic;
using UnityEngine;

namespace OpenUGD.Core
{
    /// <summary>
    /// Creates and owns the per-display <see cref="ContextInstanceComponent"/> instances, and moves
    /// focus between them. The entry point for running several game instances — one per display — in
    /// one process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Boot.</b> <c>Awake</c> marks this component's own <c>GameObject</c> <c>DontDestroyOnLoad</c> and
    /// then builds <see cref="Count"/> instances, selecting instance <c>0</c> and unselecting the rest, so
    /// exactly one instance has focus from the first frame.
    /// </para>
    /// <para>
    /// <b>The instances are not marked <c>DontDestroyOnLoad</c>.</b> Only this component is. The clones are
    /// created into the active scene, so a scene load destroys them while the factory survives. Nothing
    /// prunes the tracking list when that happens — <see cref="Selected"/> keeps reporting the index the
    /// destroyed instance last had, and <see cref="Select"/> reaches for components that are gone — so
    /// call <see cref="Rebuild"/> afterwards.
    /// </para>
    /// <para>
    /// <b>It writes to the prefab, not to a copy.</b> <see cref="Rebuild"/> assigns
    /// <see cref="ContextInstanceComponent.TargetDisplay"/> on <see cref="Prefab"/> before each
    /// <c>Instantiate</c> so the clone is born knowing its display, and restores it to <c>0</c> in a
    /// <c>finally</c>. That <c>finally</c> is what keeps a failed rebuild from leaving the asset carrying
    /// the last index it happened to reach.
    /// </para>
    /// </remarks>
    public class ContextFactoryInstancesComponent : MonoBehaviour
    {
        /// <summary>
        /// The instance prefab, cloned once per display. Required.
        /// </summary>
        /// <remarks>
        /// <see cref="Rebuild"/> dereferences it before the first <c>Instantiate</c> and again in its
        /// <c>finally</c>, so leaving it empty fails the rebuild — and therefore <c>Awake</c> — with a
        /// <see cref="System.NullReferenceException"/>.
        /// </remarks>
        [SerializeField] public ContextInstanceComponent Prefab;

        /// <summary>
        /// How many instances the next <see cref="Rebuild"/> will create.
        /// </summary>
        /// <remarks>
        /// Changing it does nothing on its own — nothing watches this field. Call <see cref="Rebuild"/>, or
        /// press the Rebuild button this package's inspector adds. The <c>[Range(1, 3)]</c> constrains the
        /// inspector slider only: code may assign anything, and a value of zero or less simply builds no
        /// instances, after which <see cref="Selected"/> is <c>-1</c> and <see cref="Select"/> has nothing
        /// to move focus to.
        /// </remarks>
        [Range(1, 3)] [SerializeField] public int Count = 2;

        private readonly List<ContextInstanceComponent> _instances = new();

        /// <summary>
        /// The index of the instance that currently holds focus, or <c>-1</c> when none does.
        /// </summary>
        /// <remarks>
        /// A linear scan of the instances, not a cached field, so it stays truthful even when focus was
        /// changed on a <see cref="ContextInstanceComponent"/> directly rather than through
        /// <see cref="Select"/>. <c>-1</c> is a real answer, not an error: before <c>Awake</c>, after a
        /// rebuild that created nothing, and in the window inside <see cref="Select"/> between unselecting
        /// the outgoing instance and selecting the incoming one.
        /// </remarks>
        public int Selected => _instances.FindIndex(s => s.IsSelected);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Rebuild();
        }

        /// <summary>
        /// Moves focus to instance <paramref name="index"/>: unselects whichever instance currently holds
        /// it, then selects that one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Ordering.</b> The outgoing instance's listeners run before the incoming instance's, so there
        /// is a moment during this call when <see cref="Selected"/> is <c>-1</c> and no event system is
        /// enabled. A listener that reads focus during the switch sees that gap.
        /// </para>
        /// <para>
        /// <b>Out-of-range is a deliberate no-op</b> for <c>-1</c> and for any index at or beyond the
        /// number of instances, so a UI wired to a fixed set of buttons need not know how many instances
        /// exist. Re-selecting the instance that already holds focus is a no-op too — nothing is
        /// unselected and no listener runs. An index below <c>-1</c> is not covered by that guard, and
        /// it throws.
        /// </para>
        /// <para>
        /// <b>If a listener on the outgoing instance throws, the incoming instance is never selected</b> and
        /// the switch is left half-done, with nothing focused. Keep focus listeners from throwing, or
        /// re-drive this after catching.
        /// </para>
        /// </remarks>
        /// <param name="index">
        /// Zero-based instance index. It matches both <see cref="Selected"/> and the instance's
        /// <see cref="ContextInstanceComponent.TargetDisplay"/>, because instances are created in display
        /// order.
        /// </param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than <c>-1</c>.
        /// </exception>
        /// <exception cref="System.AggregateException">
        /// A focus listener on either instance threw; see the note above about the half-done switch.
        /// </exception>
        public void Select(int index)
        {
            if (_instances != null && _instances.Count > index && index != -1 &&
                !_instances[index].IsSelected)
            {
                var current = _instances.Find(a => a.IsSelected);
                if (current != null)
                {
                    current.Unselect();
                }

                _instances[index].Select();
            }
        }

        /// <summary>
        /// Destroys the current instances and creates <see cref="Count"/> fresh ones, selecting instance
        /// <c>0</c> and unselecting the rest. Called from <c>Awake</c>, and from the Rebuild button this
        /// package's inspector adds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Destruction is deferred, creation is not.</b> The old objects go through Unity's
        /// <c>Destroy</c>, which runs at the end of the frame, while the replacements exist immediately —
        /// so for the remainder of the current frame both sets are alive, with two enabled event systems
        /// and two enabled audio listeners. The tracking list is cleared first, so
        /// <see cref="Selected"/> and <see cref="Select"/> only ever see the new set.
        /// </para>
        /// <para>
        /// <b>Every focus subscription is dropped.</b> Each destroyed instance terminates its own scope in
        /// <c>OnDestroy</c>, which detaches everything registered through
        /// <see cref="ContextInstanceComponent.Subscribe"/>. Anything watching focus has to re-subscribe to
        /// the new instances; nothing is migrated for you.
        /// </para>
        /// <para>
        /// Clones are named <c>&lt;prefab&gt;_&lt;index&gt;</c> and carry
        /// <see cref="ContextInstanceComponent.TargetDisplay"/> equal to their index.
        /// </para>
        /// </remarks>
        /// <exception cref="System.NullReferenceException"><see cref="Prefab"/> is unassigned.</exception>
        /// <exception cref="System.AggregateException">
        /// A focus listener on one of the new instances threw while it was being selected or unselected.
        /// The instances built so far are kept and tracked, the remaining ones are never created, and
        /// <see cref="Prefab"/>'s index is still restored on the way out.
        /// </exception>
        public void Rebuild()
        {
            DeleteLast();
            Create(Count);
        }

        private void Create(int count)
        {
            try
            {
                for (var i = 0; i < count; i++)
                {
                    Prefab.TargetDisplay = i;
                    var instance = Instantiate(Prefab);
                    instance.gameObject.name = $"{Prefab.gameObject.name}_{i}";
                    _instances.Add(instance);
                    if (i == 0)
                    {
                        instance.Select();
                    }
                    else
                    {
                        instance.Unselect();
                    }
                }
            }
            finally
            {
                Prefab.TargetDisplay = 0;
            }
        }

        private void DeleteLast()
        {
            var copy = _instances.ToArray();
            _instances.Clear();
            foreach (var instance in copy)
            {
                Destroy(instance.gameObject);
            }
        }
    }
}
