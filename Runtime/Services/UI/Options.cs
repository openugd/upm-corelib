// © 2025 OpenUGD

using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI
{
    public class Options : Dictionary<string, object>
    {
        public static class OptionsKeys
        {
            public const string Path = nameof(Path);
            public const string Provider = nameof(Provider);
            public const string Injector = nameof(Injector);
        }

        public Options SetPath(string path)
        {
            this[OptionsKeys.Path] = path;
            return this;
        }

        public string Path {
            get => this[OptionsKeys.Path] as string;
            set => SetPath(value);
        }

        public Options SetProvider(Func<IUIComponentProvider> provider)
        {
            this[OptionsKeys.Provider] = provider;
            return this;
        }

        public Func<IUIComponentProvider> Provider {
            get => this[OptionsKeys.Provider] as Func<IUIComponentProvider>;
            set => SetProvider(value);
        }

        public Options SetInjector(IInjector injector)
        {
            this[OptionsKeys.Injector] = injector;
            return this;
        }

        public IInjector Injector {
            get => this[OptionsKeys.Injector] as IInjector;
            set => SetInjector(value);
        }
    }
}
