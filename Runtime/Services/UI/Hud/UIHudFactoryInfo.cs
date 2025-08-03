using System;

namespace OpenUGD.Services.UI.Hud
{
    public class UIHudFactoryInfo
    {
        private readonly Action<HudOptions> _setupOptions;
        private readonly HudOptions _options;
        private bool _optionsResolved = false;

        public HudOptions Options
        {
            get
            {
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

        public UIHudFactoryInfo(
            Type type,
            HudOptions options,
            Action<HudOptions> setupOptions
        )
        {
            Type = type;
            _setupOptions = setupOptions;
            _options = options;
        }
    }
}