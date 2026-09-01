// A lambda's `async` goes in a different place in each of its spellings and its return type comes
// from a conversion the rule would have to re-check after the edit. Not matched at all.

using System;
using System.Threading.Tasks;

public sealed class Store {
    public Func<Task<int>> Build() => async () => await LoadAsync();

    static Task<int> LoadAsync() => Task.FromResult(1);
}
