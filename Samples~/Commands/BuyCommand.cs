using System;
using OpenUGD.Commands;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Commands
{
    /// <summary>
    /// Registered by type: built fresh for every <see cref="BuyMessage"/>. Its constructor takes the message, the
    /// execution's <see cref="Lifetime"/> and two services; its <c>[Inject]</c> member is a service too. All of
    /// that is checked when the command is registered, not when the first message arrives.
    /// </summary>
    public sealed class BuyCommand : ICommand
    {
        private readonly Lifetime _execution;
        private readonly IInventory _inventory;
        private readonly BuyMessage _message;
        private readonly IWallet _wallet;

        [Inject] private ILog _log;

        public BuyCommand(BuyMessage message, IWallet wallet, IInventory inventory, Lifetime execution)
        {
            _message = message;
            _wallet = wallet;
            _inventory = inventory;
            _execution = execution;
        }

        public void Execute()
        {
            // Subscribed for this execution only: the execution's Lifetime ends when Execute returns or throws.
            _wallet.Changed.Subscribe(_execution, coins => _log.Debug($"balance during the purchase: {coins}"));

            // A refusal is an exception: the caller of Tell receives it as itself.
            if (!_wallet.TrySpend(_message.Price))
            {
                throw new InvalidOperationException(
                    $"{_message.Item} costs {_message.Price} coins and the wallet holds {_wallet.Coins}.");
            }

            _inventory.Add(_message.Item);
            _log.Info($"bought {_message.Item} for {_message.Price}; {_wallet.Coins} coins left");
        }
    }
}
