using System.Collections.ObjectModel;
using System.Linq;

public sealed class Registry {
    // ⚠ This is the fixture that actually holds the receiver set down. `Collection<T>` binds
    // `Enumerable.ElementAt` like a `List<T>` does and declares an `int` indexer like a `List<T>`
    // does, so every guard before the receiver set passes and only the closed list refuses it.
    // Without it, widening that list is a change no test can see.
    public static int Third(Collection<int> entries) => entries.ElementAt(2);
}
