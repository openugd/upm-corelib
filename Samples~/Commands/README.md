# Commands

A shop wired with the command map: messages mapped to commands by type, by factory and as a one-time
registration, a registration undone, a refusal reported to the caller of `Tell`, and a listener that hears every
message.

## How to run it

1. Create an empty scene.
2. Add an empty GameObject and put `CommandsSample` on it.
3. Press Play and read the Console.

The whole sequence runs in `OnStarted`. Expected output, without the `Commands.` tag prefix:

```text
Sample->1. the starter pack, twice: the one-time command runs once
->starter pack: 100 coins and a free stick
->balance during the purchase: 100
->bought Stick for 0; 100 coins left
Trace->BuyMessage
Trace->GrantStarterPackMessage
Trace->GrantStarterPackMessage
Sample->2. buy a sword
->balance during the purchase: 40
->bought Sword for 60; 40 coins left
Trace->BuyMessage
Sample->3. buy a shield the wallet cannot pay for: Tell rethrows the command's exception as itself
Trace->BuyMessage
Sample->refused: Shield costs 60 coins and the wallet holds 40.
Sample->4. refund the sword through the factory-built command
Shop->refunded Sword: 100 coins
Trace->RefundMessage
Sample->5. close the shop, then try to buy again: no command handles it
Shop->closed: purchases are no longer handled
Trace->BuyMessage
Sample->done: 100 coins; inventory: Stick
```

Read it with these rules in mind:

- **The starter pack runs once.** It is registered `oneTime`, so the second `GrantStarterPackMessage` reaches no
  command. Its `Execute` tells a `BuyMessage` for the free stick; that nested `Tell` runs to completion inside it.
- **Listeners hear a message after its commands.** That is why the trace shows the nested `BuyMessage` before the
  `GrantStarterPackMessage` that caused it.
- **A refusal is an exception.** `BuyCommand` throws when the wallet cannot pay. Every other command and listener
  still runs — the trace line comes first — and then `Tell` rethrows that exception as itself.
- **Each execution has a scope.** `BuyCommand` subscribes to the wallet on its execution's `Lifetime`, so the
  "balance during the purchase" handler is gone once the purchase returns.
- **A registration is undone by terminating it.** `ShopCommands.CloseShop` terminates the `Lifetime.Definition`
  that `Map<BuyMessage, BuyCommand>()` returned; the last `BuyMessage` is heard only by the trace.

## What to look at

| File | What it shows |
| --- | --- |
| `ShopCommands.cs` | **Start here.** A module maps its messages while the context boots: by type (checked at registration), one-time, and by factory; and keeps one registration to undo later. |
| `BuyCommand.cs` | A command registered by type: the message, two services and the execution's `Lifetime` as constructor parameters, an `[Inject]` member, and a refusal thrown as an exception. |
| `GrantStarterPackCommand.cs` | A one-time command that tells a message from `Execute`. |
| `RefundCommand.cs` | A command built by a factory: an `internal` constructor and no reflection. |
| `MessageTrace.cs` | An `ITellMessage` listener, subscribed with `CommandMap.Subscribe`. |
| `CommandsSample.cs` | The context (`AddCommandMap` and the services) and the scripted sequence. |
| `ShopMessages.cs`, `Wallet.cs` | The messages, and the two services the commands use. |

Everything but `CommandsSample` is plain C#: the commands assembly does not reference UnityEngine.

This is sample code: once imported it is yours to change, and nothing in the package depends on it.
