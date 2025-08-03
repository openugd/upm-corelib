using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Hud
{
    public class UIHudReference
    {
        private readonly Lifetime.Definition _definition;

        public UIHudReference(Lifetime.Definition definition) => _definition = definition;

        public Widget Widget { get; internal set; }

        public void Close() => _definition.Terminate();
    }
}