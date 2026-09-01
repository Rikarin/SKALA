using System.Threading.Tasks;

namespace Contoso.Design;

// An `async void` method is asynchronous — unawaitable, which is `SK3001`'s finding, not this one — so
// the suffix it carries is earned and the `void` is not evidence against it. A local function is
// excluded for a different reason: its name is a detail of one body with no callers outside it, so the
// suffix carries no information there.
public sealed class Widget {
    public async void OnClickAsync() {
        await Task.Yield();
    }

    public int Run() {
        Task<int> Compute() => Task.FromResult(1);

        return Compute().Result;
    }
}
