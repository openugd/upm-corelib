namespace OpenUGD.Services.UI.Hud
{
    public class UIHudReference
    {
        private readonly Lifetime.Definition _definition;

        public UIHudReference(Lifetime.Definition definition) => _definition = definition;

        public void Close() => _definition.Terminate();
    }
}
