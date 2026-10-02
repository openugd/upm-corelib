using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>Registers the window service and the windows it can open.</summary>
    /// <remarks>
    /// These take a <see cref="ContextBuilder"/> rather than a <see cref="ServiceCollection"/> because a
    /// window registration has to reach the built <see cref="UIWindowService"/> instance, and nothing is
    /// constructed while the collection is being filled. The registration is therefore deferred to
    /// <see cref="BootPhase.Configure"/> — after every service's <c>AwakeAsync</c>, and before any
    /// <c>InitializeAsync</c> that might open a window.
    /// </remarks>
    public static class UIWindowServiceInstallerExtension
    {
        /// <summary>Registers the default window service, unless one is already registered.</summary>
        /// <param name="builder">The context builder.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        public static ContextBuilder AddWindowsService(this ContextBuilder builder,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (!builder.Services.Contains(typeof(IUIWindowService)))
            {
                builder.Services.Add<UIWindowService>(file, line)
                    .As<IUIWindowService>()
                    .As<IUIWindowsProvider>()
                    .As<IUIWindowsRegister>();
            }

            return builder;
        }

        /// <summary>Registers a window presenter and the service that opens it.</summary>
        /// <typeparam name="TPresenter">The window presenter type.</typeparam>
        /// <param name="builder">The context builder.</param>
        /// <param name="options">Configures the registration.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public static ContextBuilder AddWindow<TPresenter>(this ContextBuilder builder,
            Action<WindowOptions> options,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
            where TPresenter : Presenter
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (options == null) throw new ArgumentNullException(nameof(options));

            builder.AddWindowsService(file, line);

            var lifetime = builder.Lifetime;
            builder.Initializers.Add(BootPhase.Configure, (context, token) => {
                context.Resolve<IUIWindowsRegister>().AddWindow(
                    lifetime: lifetime,
                    type: typeof(TPresenter),
                    options: opt => options(opt.SetContext(context)));
                return Task.CompletedTask;
            }, "AddWindow<" + typeof(TPresenter).Name + "> (" + file + ":" + line + ")");

            return builder;
        }

        /// <summary>Registers a window by path.</summary>
        /// <typeparam name="TPresenter">The window presenter type.</typeparam>
        /// <param name="builder">The context builder.</param>
        /// <param name="path">The resource path of the view.</param>
        /// <param name="isFullscreen">Whether the window covers the screen.</param>
        /// <param name="provider">An explicit component provider, or null for the default.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        [Obsolete("Use AddWindow() instead.")]
        public static ContextBuilder RegisterWindow<TPresenter>(this ContextBuilder builder, string path,
            bool isFullscreen, IUIComponentProvider provider = null)
            where TPresenter : Presenter =>
            builder.AddWindow<TPresenter>(options => {
                if (provider != null)
                {
                    options.Path = path;
                    options.Provider = () => provider;
                }
                else
                {
                    options.LoadFromResources(path);
                }

                options.Fullscreen = isFullscreen;
            });
    }
}
