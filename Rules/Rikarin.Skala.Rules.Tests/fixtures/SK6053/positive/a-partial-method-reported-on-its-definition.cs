using System.Threading.Tasks;

namespace Contoso.Design;

// #400: one method written twice is one finding, on the definition — the half that is hand-written
// when a generator supplies the other. `PartialMemberTests` asserts the count and the line.
public sealed partial class Store {
    public partial Task<int> Fetch(int id);

    public partial Task<int> Fetch(int id) => Task.FromResult(id);
}
