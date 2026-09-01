using System.Threading.Tasks;

namespace Contoso.Design;

// Both directions agreeing, which is the ordinary case and has to stay silent for the rule to be
// worth turning on at all.
public sealed class Store {
    public Task<int> LoadAsync(int id) => Task.FromResult(id);

    public int Load(int id) => id;

    public async ValueTask<int> CountAsync() {
        await Task.Yield();

        return 1;
    }
}
