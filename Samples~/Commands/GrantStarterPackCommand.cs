using OpenUGD.Commands;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Commands
{
    /// <summary>
    /// Registered as a one-time command: it runs for the first <see cref="GrantStarterPackMessage"/> and is then
    /// unregistered. It tells a message of its own from <see cref="Execute"/>, which a dispatch allows.
    /// </summary>
    public sealed class GrantStarterPackCommand : ICommand
    {
        private readonly ILog _log;
        private readonly ITellMessage _tell;
        private readonly IWallet _wallet;

        public GrantStarterPackCommand(IWallet wallet, ITellMessage tell, ILog log)
        {
            _wallet = wallet;
            _tell = tell;
            _log = log;
        }

        public void Execute()
        {
            _wallet.Add(100);
            _log.Info("starter pack: 100 coins and a free stick");
            _tell.Tell(new BuyMessage("Stick", 0));
        }
    }
}
