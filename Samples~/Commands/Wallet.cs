using System.Collections.Generic;

namespace OpenUGD.Samples.Commands
{
    /// <summary>The player's coins.</summary>
    public interface IWallet
    {
        int Coins { get; }

        /// <summary>Fires with the new balance after every change.</summary>
        ISignal<int> Changed { get; }

        void Add(int coins);

        /// <summary>Spends <paramref name="coins"/> if there are enough; returns whether it did.</summary>
        bool TrySpend(int coins);
    }

    public sealed class Wallet : IWallet
    {
        private readonly Signal<int> _changed;

        public Wallet(Lifetime lifetime) => _changed = new Signal<int>(lifetime);

        public int Coins { get; private set; }

        public ISignal<int> Changed => _changed;

        public void Add(int coins)
        {
            Coins += coins;
            _changed.Fire(Coins);
        }

        public bool TrySpend(int coins)
        {
            if (coins > Coins) return false;
            Coins -= coins;
            _changed.Fire(Coins);
            return true;
        }
    }

    /// <summary>What the player owns.</summary>
    public interface IInventory
    {
        IReadOnlyList<string> Items { get; }

        void Add(string item);

        bool Remove(string item);
    }

    public sealed class Inventory : IInventory
    {
        private readonly List<string> _items = new List<string>();

        public IReadOnlyList<string> Items => _items;

        public void Add(string item) => _items.Add(item);

        public bool Remove(string item) => _items.Remove(item);
    }
}
