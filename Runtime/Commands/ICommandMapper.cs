using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenUGD.Commands
{
    /// <summary>
    /// Registers the commands that run when a single message type is told.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two ways to describe a command.</b> By its <see cref="Type"/>: the mapper checks at registration that it
    /// can be built and decides how, then builds a fresh command for every message, offering the message, the
    /// registration's <see cref="Lifetime.Definition"/> and the execution's <see cref="Lifetime"/> as constructor
    /// arguments and resolving everything else from the context. Or by a factory, which builds it from the
    /// message and the execution's <see cref="Lifetime"/> with no reflection at all. Nothing is written into the
    /// container either way.
    /// </para>
    /// <para>
    /// <b>Each execution has a scope.</b> A fresh <see cref="Lifetime"/> is defined for every message a command
    /// handles, nested in the registration's, and terminated as soon as <see cref="ICommand.Execute"/> returns or
    /// throws — or earlier, if the registration ends. Clean-up a command registers on it runs right after that
    /// command.
    /// </para>
    /// <para>
    /// <b>A registration is undone by terminating the <see cref="Lifetime.Definition"/> it returns</b>, which
    /// is an <see cref="IDisposable"/>; <see cref="ICommandMapperRemove"/> removes every registration of a type at
    /// once.
    /// </para>
    /// </remarks>
    public interface ICommandMapper
    {
        /// <summary>
        /// Registers <paramref name="commandType"/> to run whenever this mapper's message is told.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Checked now.</b> The constructor is chosen as <c>Context.Instantiate</c> chooses one — the one marked
        /// <c>[Inject]</c>, else the widest public constructor that can be satisfied — from the message, a
        /// <see cref="Lifetime.Definition"/> (the registration's), a <see cref="Lifetime"/> (the execution's) and
        /// the context's services. Every <c>[Inject]</c> field and property must be assignable, and, unless it is
        /// optional, resolvable from the context; members are not offered the message or the lifetimes. If any of
        /// this fails, this throws, rather than every <c>Tell</c> failing later.
        /// </para>
        /// <para>
        /// <b>Cost per message.</b> The choice is made once, here. Each <c>Tell</c> resolves the constructor's
        /// services, calls the chosen constructor through reflection, and fills the <c>[Inject]</c> members, if the
        /// type has any, with <c>Context.Inject</c>. A command that throws from its constructor fails with that
        /// exception, as it would from a factory.
        /// </para>
        /// <para>
        /// <b>Managed code stripping (IL2CPP).</b> Nothing in a player calls a command's constructor except this
        /// mapper, by reflection, so the linker keeps it only because this parameter is annotated for it: a type
        /// written at the call — <c>typeof(BuyCommand)</c>, or the type argument of
        /// <see cref="CommandMapperExtensions.RegisterCommand{TCommand}"/> and
        /// <see cref="CommandMapperExtensions.Map{TMessage, TCommand}"/> — keeps its constructors. A type the
        /// linker cannot trace, read from data or passed on by an unannotated generic method of your own, does
        /// not: put <c>[Inject]</c> on the command's constructor, as the <c>com.openugd.context</c> README
        /// describes.
        /// </para>
        /// </remarks>
        /// <param name="commandType">
        /// A concrete class implementing <see cref="ICommand"/>, not derived from <c>UnityEngine.Object</c>.
        /// Instantiated once per message.
        /// </param>
        /// <param name="oneTime">
        /// When <see langword="true"/> the registration runs once — not again from a dispatch that its own command
        /// starts — and is terminated after that run, whether or not <see cref="ICommand.Execute"/> threw.
        /// </param>
        /// <returns>
        /// The registration. Terminate (or dispose) it to unregister the command; it also ends with this mapper's
        /// scope. It converts implicitly to its <see cref="Lifetime"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="commandType"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="commandType"/> is not a concrete class implementing <see cref="ICommand"/>, derives from
        /// <c>UnityEngine.Object</c>, has no constructor that can be satisfied here or two equally wide ones that
        /// can, or has an <c>[Inject]</c> member that cannot be filled; the message names the parameter or member.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This mapper's scope has already terminated, so the registration could never run.
        /// </exception>
        Lifetime.Definition RegisterCommand([DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType,
            bool oneTime = false);

        /// <summary>
        /// Registers a factory that builds the command to run whenever this mapper's message is told. No
        /// reflection: the factory is called once per message.
        /// </summary>
        /// <param name="factory">
        /// Builds the command from the message (an instance of this mapper's message type) and the execution's
        /// <see cref="Lifetime"/>, which ends when the command's <see cref="ICommand.Execute"/> returns. Must not
        /// return <c>null</c>; if it throws, or returns <c>null</c>, that execution fails like a throwing command.
        /// The command is run as returned: its <c>[Inject]</c> members are not filled, so hand it what it needs.
        /// </param>
        /// <param name="oneTime">
        /// When <see langword="true"/> the registration runs once — not again from a dispatch that its own command
        /// starts — and is terminated after that run, whether or not it threw.
        /// </param>
        /// <returns>The registration, as for <see cref="RegisterCommand(Type, bool)"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// This mapper's scope has already terminated, so the registration could never run.
        /// </exception>
        Lifetime.Definition RegisterCommand(Func<object, Lifetime, ICommand> factory, bool oneTime = false);
    }
}
