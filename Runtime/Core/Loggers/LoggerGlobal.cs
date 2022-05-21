using System;
using System.Collections.Generic;

namespace OpenUGD.Core.Loggers
{
    public class LoggerGlobal : Logger
    {
        private readonly LoggerImpl _impl;
        private readonly List<ILoggerProvider> _loggers = new();

        public LoggerGlobal(string tag = null) => _impl = new LoggerImpl(tag ?? "", null, this);

        public Logger Parent { get; } = null;
        public LoggerFlag Flag { get; set; } = LoggerFlag.All;
        public LoggerFlag LogFlag => Flag;

        public Logger WithTag(Type type) => _impl.WithTag(type);

        public Logger WithTag(string tag) => _impl.WithTag(tag);

        public void Dispose() => throw new NotImplementedException();

        public virtual Logger V(object message) => _impl.V(message);

        public virtual Logger I(object message) => _impl.I(message);

        public virtual Logger W(object message) => _impl.W(message);

        public virtual Logger E(object message) => _impl.E(message);

        public virtual Logger D(object message) => _impl.D(message);

        public virtual Logger F(object message) => _impl.F(message);

        public void Subscribe(ILoggerProvider logger) => _loggers.Add(logger);

        public void Unsubscribe(ILoggerProvider logger) => _loggers.Remove(logger);

        public Logger Log(LoggerFlag flag, string tag, object message)
        {
            if ((Flag & flag) == flag)
            {
                foreach (var logger in _loggers)
                {
                    logger.Log(flag, tag, message);
                }
            }

            return this;
        }

        private class LoggerImpl : Logger
        {
            private readonly LoggerGlobal _loggerGlobal;
            private LoggerImpl _parent;

            public LoggerImpl(string tag, LoggerImpl parent = null, LoggerGlobal loggerGlobal = null)
            {
                Tag = tag;
                _parent = parent;
                _loggerGlobal = loggerGlobal;
            }

            public string Tag { get; }

            private LoggerFlag InternalFlag {
                get {
                    if (_parent != null)
                    {
                        return _parent!.InternalFlag & Flag;
                    }

                    return Flag;
                }
            }

            private string InternalTag {
                get {
                    if (_parent != null)
                    {
                        return $"{_parent.InternalTag}.{Tag}";
                    }

                    return Tag;
                }
            }

            private LoggerGlobal Global {
                get {
                    var current = this;
                    while (current != null)
                    {
                        if (current._loggerGlobal != null)
                        {
                            return current._loggerGlobal;
                        }

                        current = current._parent;
                    }

                    return null;
                }
            }

            public LoggerFlag Flag { get; set; } = LoggerFlag.All;
            public LoggerFlag LogFlag => InternalFlag;
            public Logger Parent => _parent;

            public Logger WithTag(Type type) => WithTag(type.Name);

            public Logger WithTag(string tag) => new LoggerImpl(tag, this);

            public void Dispose() => _parent = null;

            public virtual Logger V(object message) => Log(LoggerFlag.Verbose, message);

            public virtual Logger I(object message) => Log(LoggerFlag.Info, message);

            public virtual Logger W(object message) => Log(LoggerFlag.Warning, message);

            public virtual Logger E(object message) => Log(LoggerFlag.Error, message);

            public virtual Logger D(object message) => Log(LoggerFlag.Debug, message);

            public virtual Logger F(object message) => Log(LoggerFlag.Fatal, message);

            private Logger Log(LoggerFlag flag, object message)
            {
                if ((InternalFlag & flag) == flag)
                {
                    Global.Log(flag, InternalTag, message);
                }

                return this;
            }
        }
    }
}
