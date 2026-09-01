using System;

// ⚠ The rule proves an omission. A message it cannot fold to a constant is accepted without
// inspection rather than guessed at — reporting one would be reporting the analyzer's own limit.
public sealed class Store {
    const string Reason = "Use SaveAsync; this overload blocks the calling thread.";

    [Obsolete(Reason)]
    public void Save() { }

    [Obsolete(nameof(SaveAsync) + " replaces this.")]
    public void Flush() { }

    public void SaveAsync() { }
}
