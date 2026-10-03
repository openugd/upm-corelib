namespace OpenUGD.Samples.Bootstrap
{
    /// <summary>The player's score: read it, add to it, and hear when it changes.</summary>
    public interface IScore
    {
        /// <summary>The current score.</summary>
        int Value { get; }

        /// <summary>Fires with the new score after every change.</summary>
        ISignal<int> Changed { get; }

        /// <summary>Adds <paramref name="points"/> and fires <see cref="Changed"/>.</summary>
        /// <param name="points">How many points to add; may be negative.</param>
        void Add(int points);
    }
}
