using System;

namespace OpenUGD.Services.UI.Windows
{
    public class UIWindowReference
    {
        private readonly Lifetime.Definition _definition;

        public UIWindowReference(
            Lifetime.Definition definition,
            WindowOptions options,
            Type type,
            object model
        )
        {
            _definition = definition;
            Options = options;
            Type = type;
            Model = model;
        }

        public Lifetime Lifetime => _definition.Lifetime;
        public WindowOptions Options { get; private set; }
        public Type Type { get; private set; }
        public object Model { get; private set; }

        public void Close() => _definition.Terminate();
    }
}
