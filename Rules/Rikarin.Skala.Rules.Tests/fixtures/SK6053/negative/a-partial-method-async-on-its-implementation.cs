using System.Threading.Tasks;

namespace Contoso.Design;

// #400: `async` is legal only on a partial method's implementation, so the definition alone reads as
// a method named asynchronous with nothing asynchronous about it. The two halves are one method, and
// that method is `async`.
public sealed partial class Panel {
    public partial void RefreshAsync();

    public async partial void RefreshAsync() {
        await Task.Yield();
    }
}
