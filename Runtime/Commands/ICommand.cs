namespace OpenUGD.Commands
{
    /// <summary>A unit of work run in response to one message.</summary>
    public interface ICommand
    {
        /// <summary>Runs the command. Called exactly once per message per registration.</summary>
        void Execute();
    }
}
