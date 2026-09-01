using System.Diagnostics.Contracts;
using System.Threading.Tasks;

// A `Task` is a return value. `async void` would not be, but it is also not spelled `void` here.
static class Work {
    [Pure]
    public static Task<int> CountAsync() => Task.FromResult(1);
}
