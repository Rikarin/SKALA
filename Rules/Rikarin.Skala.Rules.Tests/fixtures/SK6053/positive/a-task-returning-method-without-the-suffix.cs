using System.Threading.Tasks;

namespace Contoso.Design;

public sealed class Store {
    public Task<int> Load(int id) => Task.FromResult(id);
}
