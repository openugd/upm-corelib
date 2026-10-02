using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>Supplies the tooltip registrations a <c>UITooltipService</c> can open.</summary>
    /// <remarks>
    /// The read half of the tooltip registry; <see cref="IUITooltipRegister"/> is the write half, and
    /// <c>UITooltipServiceExtensions.AddToolTipService</c> registers one object under both. Unlike the HUD
    /// and window registries, which are consulted per open, this one is drained once at boot — so it
    /// describes the tooltips a context was built with, not a set that can grow while it runs.
    /// </remarks>
    public interface IUITooltipProvider
    {
        /// <summary>Every tooltip registration.</summary>
        /// <remarks>
        /// Called once, from the service's Initialize boot phase, and the results are copied into a
        /// type-keyed table; nothing calls it again, so a registration added afterwards is silently
        /// ignored. Two maps with the same <see cref="UITooltipMap.Type"/> are not an error — the last one
        /// enumerated wins. The built-in implementation returns its own backing list rather than a copy, so
        /// treat the result as a live view and do not mutate it while enumerating.
        /// </remarks>
        /// <returns>The registrations, in registration order. Must not be <c>null</c>: the service
        /// enumerates it without checking.</returns>
        IEnumerable<UITooltipMap> Provide();
    }

    /// <summary>Records the tooltip presenters a <c>UITooltipService</c> can open.</summary>
    /// <remarks>
    /// Reached through <c>ContextBuilder.RegisterTooltip</c>, which defers the call to the Configure boot
    /// phase — that is, before the service drains the registry in Initialize. Registering later than that
    /// is ignored rather than rejected: the entry never reaches the service's table, and opening the
    /// tooltip throws <see cref="ArgumentException"/> as though it had never been registered at all.
    /// </remarks>
    public interface IUITooltipRegister
    {
        /// <summary>Registers a tooltip presenter and where its view comes from.</summary>
        /// <remarks>
        /// Nothing is validated and nothing is replaced: the built-in implementation appends a
        /// <see cref="UITooltipMap"/> to a list, and a repeated <paramref name="type"/> resolves to
        /// whichever entry was appended last. There is deliberately no way to unregister, and — unlike
        /// <see cref="OpenUGD.Services.UI.Hud.IUIHudRegister.AddHud"/> — no lifetime to scope the entry to,
        /// because the registry is read once at boot and a context's tooltips last as long as the context.
        /// </remarks>
        /// <param name="type">The tooltip presenter type. It is instantiated from the context when the
        /// tooltip opens, and must derive from <c>Presenter</c>; nothing here checks that.</param>
        /// <param name="path">The resource path of the view, handed to the provider as
        /// <see cref="Options.Path"/>.</param>
        /// <param name="provider">What loads the view, or <c>null</c> for a fresh
        /// <see cref="UITooltipComponentProvider"/>. Whichever it is, the single instance is shared by
        /// every open of this tooltip.</param>
        void Register(Type type, string path, IUIComponentProvider provider);
    }
}
