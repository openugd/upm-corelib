namespace OpenUGD.Commands
{
    public interface IMapCommand
    {
        ICommandMapper Map<TMessage>() where TMessage : IMessage;
    }
}
