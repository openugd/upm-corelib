using System;
using UnityEngine;

namespace OpenUGD.Core
{
    /// <summary>
    /// <c>gameObject.GetLifetime()</c>: the scope of a GameObject, kept by its <see cref="LifetimeBehaviour"/>.
    /// </summary>
    public static class GameObjectLifetimeExtensions
    {
        /// <summary>
        /// Returns the GameObject's scope, adding a <see cref="LifetimeBehaviour"/> the first time. It ends when the
        /// GameObject is destroyed or the play session ends, whichever comes first.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every call on the same GameObject returns the same scope. Call it from <c>Awake</c> or later. On a
        /// GameObject that has never been active it throws rather than adding a component: Unity would neither
        /// wake that component nor tell it the object was destroyed, so its scope could never end.
        /// </para>
        /// <code>
        /// signal.Subscribe(gameObject.GetLifetime(), OnScoreChanged); // unsubscribed when the object is destroyed
        /// </code>
        /// </remarks>
        /// <param name="gameObject">The GameObject. Must be alive.</param>
        /// <returns>The GameObject's scope; already ended if the GameObject is being destroyed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="gameObject"/> is <c>null</c> or destroyed.</exception>
        /// <exception cref="InvalidOperationException">Not in play mode; or the GameObject has never been active and
        /// has no scope yet; or Unity refused to add the component, as it does to an object being
        /// destroyed.</exception>
        public static Lifetime GetLifetime(this GameObject gameObject)
        {
            // Unity's overloaded operator== reports a destroyed object as null.
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject), "the GameObject is null or has been destroyed");

            var behaviour = gameObject.GetComponent<LifetimeBehaviour>();
            if (behaviour != null) return behaviour.Lifetime;

            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    $"GetLifetime on '{gameObject.name}' needs play mode: in edit mode Unity sends neither Awake nor " +
                    "OnDestroy to the component that would keep the scope, and adding it would change the scene.");
            if (!gameObject.activeInHierarchy)
                throw new InvalidOperationException(
                    $"GetLifetime on '{gameObject.name}': the GameObject is inactive and has no scope yet. Unity " +
                    "sends Awake and OnDestroy only to an object that has been active, so a scope created now might " +
                    "never end. Activate it first, or read its lifetime from Awake or later.");

            behaviour = gameObject.AddComponent<LifetimeBehaviour>();
            if (behaviour == null)
                throw new InvalidOperationException(
                    $"GetLifetime on '{gameObject.name}': Unity did not add the LifetimeBehaviour (see the console); " +
                    "it refuses to add components to an object that is being destroyed.");

            // AddComponent ran Awake: the object is active.
            return behaviour.Lifetime;
        }

        /// <summary>
        /// The scope of <paramref name="component"/>'s GameObject; see <see cref="GetLifetime(GameObject)"/>.
        /// </summary>
        /// <param name="component">Any component on the GameObject. Must be alive.</param>
        /// <returns>The GameObject's scope.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="component"/> is <c>null</c> or destroyed.</exception>
        /// <exception cref="InvalidOperationException">As for <see cref="GetLifetime(GameObject)"/>.</exception>
        public static Lifetime GetLifetime(this Component component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component), "the component is null or has been destroyed");

            return component.gameObject.GetLifetime();
        }
    }
}
