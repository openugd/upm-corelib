using System;

namespace OpenUGD.Core.Loggers
{
    public interface ILoggerProvider
    {
        void Log(LoggerFlag flag, string tag, object message);
    }

    [Flags]
    public enum LoggerFlag
    {
        Verbose = 1,
        Info = 1 << 1,
        Warning = 1 << 2,
        Error = 1 << 3,
        Debug = 1 << 4,
        Fatal = 1 << 5,

        All = Verbose | Info | Warning | Error | Debug | Fatal
    }

    public interface Logger : IDisposable
    {
        Logger Parent { get; }
        LoggerFlag Flag { get; set; }
        LoggerFlag LogFlag { get; }
        Logger WithTag(Type type);
        Logger WithTag(string tag);

        ///verbose
        Logger V(dynamic message);

        ///info
        Logger I(dynamic message);

        ///warning
        Logger W(dynamic message);

        ///error
        Logger E(dynamic message);

        ///debug
        Logger D(dynamic message);

        ///fatal
        Logger F(dynamic message);
    }
}
