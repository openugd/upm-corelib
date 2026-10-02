using System;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>One window registration: its presenter type and its lazily applied options.</summary>
    public class UIWindowFactoryInfo
    {
        private readonly WindowOptions _options;
        private readonly Action<WindowOptions> _setupOptions;
        private bool _optionsResolved;

        /// <summary>Creates a registration.</summary>
        /// <param name="type">The presenter type.</param>
        /// <param name="options">The options instance to configure.</param>
        /// <param name="setupOptions">Applied to <paramref name="options"/> on first read.</param>
        public UIWindowFactoryInfo(Type type, WindowOptions options, Action<WindowOptions> setupOptions)
        {
            Type = type;
            _setupOptions = setupOptions;
            _options = options;
        }

        /// <summary>The options, configured on first read.</summary>
        public WindowOptions Options {
            get {
                if (!_optionsResolved)
                {
                    _optionsResolved = true;
                    _setupOptions(_options);
                }

                return _options;
            }
        }

        /// <summary>The registered presenter type.</summary>
        public Type Type { get; }
    }
}
