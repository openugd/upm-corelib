using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI.Hud
{
    /// <summary>Resolves a HUD registration by presenter type.</summary>
    /// <remarks>
    /// The read half of the HUD registry; <see cref="IUIHudRegister"/> is the write half. Both are
    /// implemented by <see cref="UIHudService"/> itself, which is why <c>AddHudService</c> registers one
    /// object under three contracts. Implement this separately to keep the registrations somewhere else —
    /// a per-scene table, say — while still opening HUDs through <see cref="IHudService"/>.
    /// </remarks>
    public interface IUIHudProvider
    {
        /// <summary>The registration for <paramref name="type"/>.</summary>
        /// <remarks>
        /// Consulted while an open is being carried out rather than when the HUD was registered, so a
        /// missing registration surfaces out of <see cref="IHudService.Open"/>. The result is the
        /// registry's own instance, shared by every open of that type, and its
        /// <see cref="UIHudFactoryInfo.Options"/> are configured the first time they are read.
        /// </remarks>
        /// <param name="type">The presenter type the HUD was registered under.</param>
        /// <returns>The registration. Must not be <c>null</c>: the caller dereferences it immediately, so
        /// returning <c>null</c> for an unknown type reports the mistake as a
        /// <see cref="NullReferenceException"/> at a place that has nothing to do with it.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Nothing is registered for <paramref name="type"/>. This is what <see cref="UIHudService"/> does,
        /// since it looks the registration up with a dictionary indexer.
        /// </exception>
        UIHudFactoryInfo Get(Type type);
    }

    /// <summary>Records the HUD presenters an <see cref="IHudService"/> can open.</summary>
    /// <remarks>
    /// The write half of the HUD registry. Reached through <c>ContextBuilder.AddHud</c>, which defers the
    /// call to the Configure boot phase so that the context needed to build the presenter already exists.
    /// Unlike the tooltip registry, this one is read per open, so a HUD registered after boot is openable.
    /// </remarks>
    public interface IUIHudRegister
    {
        /// <summary>Registers <paramref name="type"/> as an openable HUD, for as long as
        /// <paramref name="lifetime"/> is alive.</summary>
        /// <remarks>
        /// <para>
        /// <paramref name="options"/> is not applied here. It is stored and run once, against a fresh
        /// <see cref="HudOptions"/>, the first time that registration is used — so a registration costs
        /// nothing until something opens it, and the delegate must remain valid until then.
        /// </para>
        /// <para>
        /// <b>Registering a type twice replaces the earlier entry</b>, and in <see cref="UIHudService"/>
        /// that interacts badly with per-registration clean-up: the removal armed on
        /// <paramref name="lifetime"/> drops whatever is stored under <paramref name="type"/> at the time,
        /// so the end of the first registration's scope also unregisters a replacement made since.
        /// </para>
        /// </remarks>
        /// <param name="lifetime">The scope of the registration; the entry is removed when it ends.</param>
        /// <param name="type">The presenter type to register. Callers normally arrive through
        /// <c>ContextBuilder.AddHud</c>, which constrains it to a <c>Presenter</c> at compile time; nothing
        /// here re-checks it.</param>
        /// <param name="options">Configures the registration on first use — where the view comes from, and
        /// which context builds the presenter and the provider.</param>
        void AddHud(Lifetime lifetime, Type type, Action<HudOptions> options);
    }
}