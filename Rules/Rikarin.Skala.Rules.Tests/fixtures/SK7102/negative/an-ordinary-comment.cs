// The rule reads documentation trivia, not characters: this is an ordinary comment.
public sealed class Store {
    /// <summary>Clears the store.</summary>
    // <returns>Not documentation.</returns>
    public void Clear() { }
}
