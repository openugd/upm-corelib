using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Hud
{
    /// <summary>Opens HUD presenters, and registers the service and the presenters it can open.</summary>
    public static class UIHudServiceExtension
    {
        /// <summary>Opens a HUD presenter.</summary>
        /// <typeparam name="TPresenter">The HUD presenter type.</typeparam>
        /// <param name="service">The HUD service.</param>
        /// <param name="onOpen">Invoked once the presenter and its view are wired.</param>
        /// <returns>A handle that can close the HUD presenter.</returns>
        public static UIHudReference Open<TPresenter>(this IHudService service, Action<TPresenter> onOpen = null)
            where TPresenter : Presenter =>
            service.Open(typeof(TPresenter), null, w => onOpen?.Invoke((TPresenter)w));

        /// <summary>Opens a HUD presenter with a model.</summary>
        /// <typeparam name="TPresenter">The HUD presenter type.</typeparam>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <param name="service">The HUD service.</param>
        /// <param name="model">The model.</param>
        /// <param name="onOpen">Invoked once the presenter, its view and its model are wired.</param>
        /// <returns>A handle that can close the HUD presenter.</returns>
        public static UIHudReference Open<TPresenter, TModel>(this IHudService service, TModel model,
            Action<TPresenter> onOpen = null)
            where TPresenter : Presenter, IPresenterWithModel<TModel>
            where TModel : class =>
            service.Open(typeof(TPresenter), model, w => onOpen?.Invoke((TPresenter)w));

        /// <summary>Registers the default HUD service, unless one is already registered.</summary>
        /// <param name="builder">The context builder.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        public static ContextBuilder AddHudService(this ContextBuilder builder,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (!builder.Services.Contains(typeof(IHudService)))
            {
                builder.Services.Add<UIHudService>(file, line)
                    .As<IHudService>()
                    .As<IUIHudProvider>()
                    .As<IUIHudRegister>();
            }

            return builder;
        }

        /// <summary>Registers a HUD presenter and the service that opens it.</summary>
        /// <typeparam name="TPresenter">The HUD presenter type.</typeparam>
        /// <param name="builder">The context builder.</param>
        /// <param name="options">Configures the registration.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public static ContextBuilder AddHud<TPresenter>(this ContextBuilder builder, Action<HudOptions> options,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
            where TPresenter : Presenter
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (options == null) throw new ArgumentNullException(nameof(options));

            builder.AddHudService(file, line);

            var lifetime = builder.Lifetime;
            builder.Initializers.Add(BootPhase.Configure, (context, token) => {
                context.Resolve<IUIHudRegister>().AddHud(
                    lifetime: lifetime,
                    type: typeof(TPresenter),
                    options: opt => options(opt.SetContext(context)));
                return Task.CompletedTask;
            }, "AddHud<" + typeof(TPresenter).Name + "> (" + file + ":" + line + ")");

            return builder;
        }

        /// <summary>Registers a HUD presenter by path.</summary>
        /// <typeparam name="TPresenter">The HUD presenter type.</typeparam>
        /// <param name="builder">The context builder.</param>
        /// <param name="path">The resource path of the view.</param>
        /// <param name="provider">An explicit component provider, or null for the default.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        [Obsolete("Use AddHud instead.")]
        public static ContextBuilder RegisterHUD<TPresenter>(this ContextBuilder builder, string path,
            IUIComponentProvider provider = null)
            where TPresenter : Presenter =>
            builder.AddHud<TPresenter>(options => {
                if (provider != null)
                {
                    options.Path = path;
                    options.Provider = () => provider;
                }
                else
                {
                    options.LoadFromResources(path);
                }
            });
    }
}
