using System.Threading.Tasks;

namespace Contoso.Design;

// ⚠ A positive fixture rather than a negative one, and the count is what it proves. The interface
// declaration is where the name is decided and is reported; the implementation takes that name and is
// not reported a second time. A negative fixture could not carry this claim — the exclusion cannot be
// demonstrated without the declaration that makes it necessary, and that declaration is a finding.
public interface ILoader {
    Task<int> Load(int id);
}

public sealed class Store : ILoader {
    public Task<int> Load(int id) => Task.FromResult(id);
}
