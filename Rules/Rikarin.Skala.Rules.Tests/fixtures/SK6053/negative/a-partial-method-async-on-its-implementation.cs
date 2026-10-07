using System.Threading.Tasks;

namespace Contoso.Design;

// #400 suspected that a partial method's definition, which cannot carry `async`, reads as a method
// named asynchronous with nothing asynchronous about it. ⚠ Measured and refuted: this was never
// reported, and the three such findings in the sweep were each the second copy of a real one. Kept
// so that stays true.
public sealed partial class Panel {
    public partial void RefreshAsync();

    public async partial void RefreshAsync() {
        await Task.Yield();
    }
}
