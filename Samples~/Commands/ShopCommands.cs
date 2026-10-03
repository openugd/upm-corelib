using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Commands;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Commands
{
    /// <summary>
    /// The shop module: a service that maps its messages to its commands while the context boots, and can close the
    /// shop again by undoing a registration.
    /// </summary>
    public sealed class ShopCommands : IInitializeService
    {
        private readonly IInventory _inventory;
        private readonly ILog _log;
        private readonly IMapCommand _map;
        private readonly IWallet _wallet;
        private Lifetime.Definition _buying;

        public ShopCommands(IMapCommand map, IWallet wallet, IInventory inventory, ILog log)
        {
            _map = map;
            _wallet = wallet;
            _inventory = inventory;
            _log = log.WithTag("Shop");
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            // By type: checked now - a constructor parameter or [Inject] member the context cannot supply throws
            // here, during the boot, instead of on the first purchase.
            _buying = _map.Map<BuyMessage, BuyCommand>();

            // One-time: runs for the first message only, even though it tells a message from inside Execute.
            _map.Map<GrantStarterPackMessage, GrantStarterPackCommand>(oneTime: true);

            // By factory: built by your code, with no reflection, from the message and the execution's Lifetime.
            _map.Map<RefundMessage>((message, execution) => new RefundCommand(message, _wallet, _inventory, _log));

            return Task.CompletedTask;
        }

        /// <summary>Unregisters <see cref="BuyCommand"/>: later <see cref="BuyMessage"/>s reach no command.</summary>
        public void CloseShop()
        {
            _buying.Terminate();
            _log.Info("closed: purchases are no longer handled");
        }
    }
}
