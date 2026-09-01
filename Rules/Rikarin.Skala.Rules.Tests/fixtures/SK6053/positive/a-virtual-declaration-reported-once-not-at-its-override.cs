using System.Threading.Tasks;

namespace Contoso.Design;

// The same claim for inheritance: the `virtual` declaration is reported and the `override` is not,
// because an override cannot be renamed without renaming what it overrides.
public class Base {
    public virtual Task<int> Fetch(int id) => Task.FromResult(id);
}

public sealed class Store : Base {
    public override Task<int> Fetch(int id) => Task.FromResult(id + 1);
}
