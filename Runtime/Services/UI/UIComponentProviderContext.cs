using System;
using UnityEngine;

namespace OpenUGD.Services.UI
{
    public class UIComponentProviderContext : IDisposable
    {
        private readonly Lifetime.Definition _definition;

        public UIComponentProviderContext(Component component, Lifetime lifetime)
        {
            Component = component;
            _definition = Lifetime.Define(lifetime);
        }

        public Component Component { get; private set; }

        public Lifetime Lifetime => _definition.Lifetime;

        public void Dispose() => _definition.Dispose();

        [Obsolete("Use Dispose instead.")]
        public void Terminate() => _definition.Terminate();
    }
}
