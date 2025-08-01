// © 2025 OpenUGD

using System;

namespace OpenUGD.Services.UI.Windows
{
    public static class UIWindowsRegisterExtensions
    {
        [Obsolete("Use AddWindow instead.")]
        public static void Register(
            this IUIWindowsRegister register,
            Type type,
            string path,
            bool isFullscreen,
            IUIComponentProvider provider
        ) =>
            register.AddWindow(((Service)register).Lifetime, type, options => options
                .SetFullscreen(isFullscreen)
                .SetPath(path)
                .SetProvider(() => provider)
            );
    }
}
