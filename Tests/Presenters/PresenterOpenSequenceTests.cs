using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenUGD.Presenters.Tests
{
    // Presenter.Internal.Initialize -> SetModel -> SetView is the open sequence of a presenter-opening service,
    // and what replaced Presenter.Internal.Ready. The 0.6.x UI services that ran it left corelib in 2.0.0
    // (branch park/ui-services, future com.openugd.corelib.ui); the sequence stays pinned here because
    // Presenter is what any such host builds on. If it ever stops producing exactly one wiring and one render
    // with both halves present, deleting Ready was wrong and a host needs a push again.
    //
    // The sequence goes through Presenter.Internal, which the presenters assembly opens to this one only.
    [TestFixture]
    public class PresenterOpenSequenceTests
    {
        private Lifetime.Definition _definition;
        private Context _context;

        [SetUp]
        public void SetUp()
        {
            _definition = Lifetime.Eternal.DefineNested();
            _context = RunSync(() => Context.CreateBuilder(_definition.Lifetime).BuildAsync());
        }

        [TearDown]
        public void TearDown() => _definition.Terminate();

        [Test]
        public void ServiceOpenSequence_ProducesOneWiringAndOneRender_WithBothHalvesPresent()
        {
            // Exactly what the 0.6.x UIWindowService / UIHudService / UITooltipService did on open.
            var definition = _definition.Lifetime.DefineNested("open");
            var presenter = new Panel();

            Presenter.Internal.Initialize(_context, presenter, definition);

            ((IPresenterWithModel)presenter).SetModel("payload");
            var mediatorView = (IPresenterWithView)presenter;
            Assert.AreEqual(typeof(FakeView), mediatorView.ViewType);
            mediatorView.SetView(new FakeView());

            CollectionAssert.AreEqual(new[] { "initialize", "view-added", "refresh:payload" }, presenter.Log,
                "the model must already be in place when the first render runs");
        }

        [Test]
        public void ServiceOpenSequence_OnATerminatedScope_Throws_SoTheGuardMustComeFirst()
        {
            var definition = _definition.Lifetime.DefineNested("open");
            definition.Terminate();

            Assert.Throws<InvalidOperationException>(
                () => Presenter.Internal.Initialize(_context, new Panel(), definition),
                "a host must check IsTerminated before attaching, and skip the whole open, for this reason");
        }

        private static T RunSync<T>(Func<Task<T>> start, int timeoutMilliseconds = 15000)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var task = start();
                try
                {
                    if (!task.Wait(timeoutMilliseconds)) Assert.Fail("did not complete");
                }
                catch (AggregateException)
                {
                }

                return task.GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        private sealed class FakeView
        {
        }

        private sealed class Panel : Presenter<FakeView, string>
        {
            public List<string> Log { get; } = new List<string>();

            protected override void OnInitialize() => Log.Add("initialize");

            protected override void OnViewAdded() => Log.Add("view-added");

            protected override void OnRefresh() => Log.Add("refresh:" + Model);
        }
    }
}
