using System;

namespace OpenUGD.Core
{
    public abstract class Context : IContext
    {
        protected Context(Lifetime lifetime) => Lifetime = lifetime;

        public Lifetime Lifetime { get; }

        public abstract object Resolve(Type type);
    }
}
