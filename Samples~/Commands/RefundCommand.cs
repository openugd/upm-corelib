using OpenUGD.Commands;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Commands
{
    /// <summary>
    /// Registered with a factory that calls this constructor: no reflection when the message is told, and no
    /// public constructor needed.
    /// </summary>
    public sealed class RefundCommand : ICommand
    {
        private readonly IInventory _inventory;
        private readonly ILog _log;
        private readonly RefundMessage _message;
        private readonly IWallet _wallet;

        internal RefundCommand(RefundMessage message, IWallet wallet, IInventory inventory, ILog log)
        {
            _message = message;
            _wallet = wallet;
            _inventory = inventory;
            _log = log;
        }

        public void Execute()
        {
            if (!_inventory.Remove(_message.Item))
            {
                _log.Warn($"nothing to refund: no {_message.Item} in the inventory");
                return;
            }

            _wallet.Add(_message.Price);
            _log.Info($"refunded {_message.Item}: {_wallet.Coins} coins");
        }
    }
}
