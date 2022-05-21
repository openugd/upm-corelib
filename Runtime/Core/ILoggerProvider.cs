// © 2025 OpenUGD

using OpenUGD.Core.Loggers;

namespace OpenUGD.Core
{
    public interface ILoggerProvider
    {
        Logger Logger { get; }
    }
}
