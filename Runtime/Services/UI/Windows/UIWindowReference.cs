using System;

namespace OpenUGD.Services.UI.Windows
{
    public class UIWindowReference
    {
        private readonly Lifetime.Definition _definition;

        public UIWindowReference(Lifetime.Definition definition, bool isFullscreen, Type type, object model)
        {
            _definition = definition;
            IsFullscreen = isFullscreen;
            Type = type;
            Model = model;
        }

        public Lifetime Lifetime => _definition.Lifetime;

        public bool IsFullscreen { get; private set; }
        public Type Type { get; private set; }
        public object Model { get; private set; }

        public void Close() => _definition.Terminate();
    }
}
