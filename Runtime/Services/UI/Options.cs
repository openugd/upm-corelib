// © 2025 OpenUGD

using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// The per-registration settings a UI service hands to its <see cref="IUIComponentProvider"/>.
    /// </summary>
    /// <remarks>
    /// Still a string-keyed dictionary. Stage 5 replaces it with a typed <c>PresenterOptions</c>; it is left
    /// alone here so this stage changes exactly one thing — the injector becomes a
    /// <see cref="OpenUGD.Context"/>.
    /// </remarks>
    public class Options : Dictionary<string, object>
    {
        /// <summary>The dictionary keys this type reads and writes.</summary>
        public static class OptionsKeys
        {
            /// <summary>Key for <see cref="Options.Path"/>.</summary>
            public const string Path = nameof(Path);

            /// <summary>Key for <see cref="Options.Provider"/>.</summary>
            public const string Provider = nameof(Provider);

            /// <summary>Key for <see cref="Options.Context"/>.</summary>
            public const string Context = nameof(Context);
        }

        /// <summary>Sets the resource path the view is loaded from.</summary>
        /// <param name="path">The path.</param>
        /// <returns>This instance, for chaining.</returns>
        public Options SetPath(string path)
        {
            this[OptionsKeys.Path] = path;
            return this;
        }

        /// <summary>The resource path the view is loaded from, or null if none was set.</summary>
        public string Path {
            get => Read(OptionsKeys.Path) as string;
            set => SetPath(value);
        }

        /// <summary>Sets the factory that produces the component provider for this registration.</summary>
        /// <param name="provider">The factory.</param>
        /// <returns>This instance, for chaining.</returns>
        public Options SetProvider(Func<IUIComponentProvider> provider)
        {
            this[OptionsKeys.Provider] = provider;
            return this;
        }

        /// <summary>The factory that produces the component provider, or null if none was set.</summary>
        public Func<IUIComponentProvider> Provider {
            get => Read(OptionsKeys.Provider) as Func<IUIComponentProvider>;
            set => SetProvider(value);
        }

        /// <summary>Sets the context presenters and providers for this registration are built from.</summary>
        /// <param name="context">The context.</param>
        /// <returns>This instance, for chaining.</returns>
        public Options SetContext(OpenUGD.Context context)
        {
            this[OptionsKeys.Context] = context;
            return this;
        }

        /// <summary>The context presenters and providers are built from, or null if none was set.</summary>
        public OpenUGD.Context Context {
            get => Read(OptionsKeys.Context) as OpenUGD.Context;
            set => SetContext(value);
        }

        private object Read(string key)
        {
            object value;
            return TryGetValue(key, out value) ? value : null;
        }
    }
}
