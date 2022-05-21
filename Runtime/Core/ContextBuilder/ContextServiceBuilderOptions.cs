using System.Collections.Generic;

namespace OpenUGD.Core.ContextBuilder
{
    public class ContextServiceBuilderOptions : Dictionary<string, object>
    {
        public ContextServiceBuilderOptions()
        {
        }

        public ContextServiceBuilderOptions(IDictionary<string, object> dictionary) : base(dictionary)
        {
        }

        public ContextServiceInitializationStrategy InitializationStrategy {
            get {
                if (!TryGetValue(nameof(InitializationStrategy), out var value))
                {
                    return ContextServiceInitializationStrategy.Parallel;
                }

                return (ContextServiceInitializationStrategy)value;
            }
            set => this[nameof(InitializationStrategy)] = value;
        }
    }
}
