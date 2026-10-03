using OpenUGD.Commands;

namespace OpenUGD.Samples.Commands
{
    /// <summary>The player asks to buy <see cref="Item"/> for <see cref="Price"/> coins.</summary>
    public sealed class BuyMessage : IMessage
    {
        public BuyMessage(string item, int price)
        {
            Item = item;
            Price = price;
        }

        public string Item { get; }

        public int Price { get; }
    }

    /// <summary>The player returns <see cref="Item"/> for a refund.</summary>
    public sealed class RefundMessage : IMessage
    {
        public RefundMessage(string item, int price)
        {
            Item = item;
            Price = price;
        }

        public string Item { get; }

        public int Price { get; }
    }

    /// <summary>A new player gets the starter pack. Only the first one counts.</summary>
    public sealed class GrantStarterPackMessage : IMessage
    {
    }
}
