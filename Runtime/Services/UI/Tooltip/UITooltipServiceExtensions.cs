using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>Registers the tooltip service and the tooltips it can open.</summary>
    public static class UITooltipServiceExtensions
    {
        /// <summary>Registers the default tooltip service, unless one is already registered.</summary>
        /// <param name="builder">The context builder.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        public static ContextBuilder AddToolTipService(this ContextBuilder builder,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            if (!builder.Services.Contains(typeof(IUITooltipProvider)))
            {
                builder.Services.AddInstance<IUITooltipProvider>(new TooltipProvider(), file, line)
                    .As<IUITooltipRegister>();
            }

            if (!builder.Services.Contains(typeof(UITooltipService)))
            {
                builder.Services.Add<UITooltipService>(file, line);
            }

            return builder;
        }

        /// <summary>Registers a tooltip presenter and the service that opens it.</summary>
        /// <typeparam name="TPresenter">The tooltip presenter type.</typeparam>
        /// <param name="builder">The context builder.</param>
        /// <param name="path">The resource path of the view.</param>
        /// <param name="provider">An explicit component provider, or null for the default.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="builder"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        public static ContextBuilder RegisterTooltip<TPresenter>(this ContextBuilder builder, string path,
            IUIComponentProvider provider = null,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
            where TPresenter : Presenter
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddToolTipService(file, line);

            builder.Initializers.Add(BootPhase.Configure, (context, token) => {
                context.Resolve<IUITooltipRegister>().Register(typeof(TPresenter), path, provider);
                return Task.CompletedTask;
            }, "RegisterTooltip<" + typeof(TPresenter).Name + "> (" + file + ":" + line + ")");

            return builder;
        }

        private class TooltipProvider : IUITooltipProvider, IUITooltipRegister
        {
            private readonly List<UITooltipMap> _list = new();

            public IEnumerable<UITooltipMap> Provide() => _list;

            public void Register(Type type, string path, IUIComponentProvider provider) =>
                _list.Add(new UITooltipMap(type, path, provider));
        }
    }
}
