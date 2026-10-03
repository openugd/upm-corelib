using System.Collections.Generic;
using NUnit.Framework;

namespace OpenUGD.Presenters.Tests
{
    // Presenter.Attach -> SetModel -> SetView is the open sequence of a presenter-opening service, and what
    // replaced Presenter.Internal.Ready. The 0.6.x UI services that ran it left corelib in 2.0.0 (branch
    // park/ui-services, future com.openugd.corelib.ui); the sequence stays pinned here because Presenter is what
    // any such host builds on. If it ever stops producing exactly one wiring and one render with both halves
    // present, deleting Ready was wrong and a host needs a push again.
    //
    // Everything here is public API: a host in another assembly can do exactly this, with no InternalsVisibleTo.
    [TestFixture]
    public class PresenterOpenSequenceTests
    {
        private Lifetime.Definition _definition;

        [SetUp]
        public void SetUp() => _definition = Lifetime.Eternal.DefineNested();

        [TearDown]
        public void TearDown() => _definition.Terminate();

        [Test]
        public void ServiceOpenSequence_ProducesOneWiringAndOneRender_WithBothHalvesPresent()
        {
            // Exactly what the 0.6.x UIWindowService / UIHudService / UITooltipService did on open, through the
            // public seams: the host's own factory, one scope per open, Attach.
            var factory = new RecordingFactory();
            var definition = _definition.Lifetime.DefineNested("open");
            var presenter = (Panel)factory.Create(typeof(Panel));

            Presenter.Attach(presenter, definition, factory);

            ((IPresenterWithModel)presenter).SetModel("payload");
            var mediatorView = (IPresenterWithView)presenter;
            Assert.AreEqual(typeof(FakeView), mediatorView.ViewType);
            mediatorView.SetView(new FakeView());

            CollectionAssert.AreEqual(new[] { "initialize", "view-added", "refresh:payload" }, presenter.Log,
                "the model must already be in place when the first render runs");

            definition.Terminate();

            Assert.IsTrue(presenter.Lifetime.IsTerminated, "ending the open's scope closes the presenter");
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
