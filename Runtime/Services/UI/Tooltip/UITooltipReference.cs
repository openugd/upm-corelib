namespace OpenUGD.Services.UI.Tooltip
{
    public class UITooltipReference
    {
        private readonly Lifetime.Definition _definition;

        public UITooltipReference(Lifetime.Definition definition) => _definition = definition;

        public void Close() => _definition.Terminate();
    }
}
