using System;

namespace OpenUGD.Services.UI.Hud
{
    /// <summary>One HUD registration: its presenter type and its lazily applied options.</summary>
    public class UIHudFactoryInfo
    {
        private readonly HudOptions _options;
        private readonly Action<HudOptions> _setupOptions;
        private bool _optionsResolved;

        /// <summary>Creates a registration.</summary>
        /// <param name="type">The presenter type.</param>
        /// <param name="options">The options instance to configure.</param>
        /// <param name="setupOptions">Applied to <paramref name="options"/> on first read.</param>
        public UIHudFactoryInfo(Type type, HudOptions options, Action<HudOptions> setupOptions)
        {
            Type = type;
            _setupOptions = setupOptions;
            _options = options;
        }

        /// <summary>The options, configured on first read.</summary>
        /// <remarks>
        /// 0.6.1 also called <c>Injector.ToFactory(Type)</c> here, so that <c>Resolve(type)</c> would
        /// construct a presenter. Nothing registers the presenter any more: the service builds it with
        /// <see cref="Context.Instantiate(Type, object[])"/>, which needs no registration.
        /// </remarks>
        public HudOptions Options {
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
