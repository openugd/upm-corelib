using System;

namespace OpenUGD.Commands
{
    public interface ICommandMapper
    {
        Lifetime RegisterCommand(Func<Lifetime, ICommand> factory, bool oneTime = false);
    }
}
