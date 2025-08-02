using System;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public class UIWindowFactoryInfo
    {
        private readonly Action<WindowOptions> _setupOptions;
        private readonly WindowOptions _options;
        private bool _optionsResolved = false;

        public WindowOptions Options {
            get {
                if (!_optionsResolved)
                {
                    _optionsResolved = true;
                    _setupOptions(_options);
                    _options.Injector.ToFactory(Type);
                }

                return _options;
            }
        }

        public Type Type { get; }

        public UIWindowFactoryInfo(
            Type type,
            WindowOptions options,
            Action<WindowOptions> setupOptions
        )
        {
            Type = type;
            _setupOptions = setupOptions;
            _options = options;
        }
    }
}
