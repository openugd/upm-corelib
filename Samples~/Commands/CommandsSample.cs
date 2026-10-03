using System;
using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Commands;
using OpenUGD.Core;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Commands
{
    /// <summary>
    /// The entry point: put it on an empty GameObject and press Play. It boots a context with a command map and
    /// the shop module, then tells a scripted sequence of messages and logs what each one did.
    /// </summary>
    public sealed class CommandsSample : ContextBehaviour
    {
        protected override bool PersistAcrossScenes => false;

        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            var log = new LogRoot("Commands");
            log.UseUnityConsole(Lifetime);

            var builder = Context.CreateBuilder(Lifetime);
            builder.Services.AddInstance<ILog>(log);
            builder.Services.AddCommandMap();
            builder.Services.Add<Wallet>().As<IWallet>();
            builder.Services.Add<Inventory>().As<IInventory>();
            builder.Services.Add<ShopCommands>();
            return builder.BuildAsync(cancellationToken);
        }

        protected override void OnStarted(Context context)
        {
            var log = context.Resolve<ILog>().WithTag("Sample");

            // Subscribe is on CommandMap itself, which AddCommandMap registers as IMapCommand and ITellMessage.
            var map = (CommandMap)context.Resolve<IMapCommand>();
            map.Subscribe(context.Lifetime, new MessageTrace(context.Resolve<ILog>()));

            log.Info("1. the starter pack, twice: the one-time command runs once");
            context.Tell(new GrantStarterPackMessage());
            context.Tell(new GrantStarterPackMessage());

            log.Info("2. buy a sword");
            context.Tell(new BuyMessage("Sword", 60));

            log.Info("3. buy a shield the wallet cannot pay for: Tell rethrows the command's exception as itself");
            try
            {
                context.Tell(new BuyMessage("Shield", 60));
            }
            catch (InvalidOperationException refused)
            {
                log.Warn($"refused: {refused.Message}");
            }

            log.Info("4. refund the sword through the factory-built command");
            context.Tell(new RefundMessage("Sword", 60));

            log.Info("5. close the shop, then try to buy again: no command handles it");
            context.Resolve<ShopCommands>().CloseShop();
            context.Tell(new BuyMessage("Shield", 60));

            var inventory = context.Resolve<IInventory>();
            log.Info($"done: {context.Resolve<IWallet>().Coins} coins; inventory: {string.Join(", ", inventory.Items)}");
        }
    }
}
