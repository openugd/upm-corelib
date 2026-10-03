using System;
using System.Collections.Generic;

namespace OpenUGD.Presenters.Tests
{
    // A hand-written IPresenterFactory: what a presenter tree needs from a container, and nothing more. The suite
    // builds every tree with it, so com.openugd.presenters is exercised with no container at all.
    internal sealed class RecordingFactory : IPresenterFactory
    {
        public List<Presenter> Injected { get; } = new List<Presenter>();

        public List<Type> Created { get; } = new List<Type>();

        // Runs inside Inject, after the presenter is recorded: lets a test fill a member, or throw.
        public Action<Presenter> OnInject { get; set; }

        public Presenter Create(Type presenterType)
        {
            Created.Add(presenterType);
            return (Presenter)Activator.CreateInstance(presenterType);
        }

        public void Inject(Presenter presenter)
        {
            Injected.Add(presenter);
            OnInject?.Invoke(presenter);
        }
    }
}
