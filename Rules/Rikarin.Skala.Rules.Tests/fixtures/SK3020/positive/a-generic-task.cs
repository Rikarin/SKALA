using System.Threading.Tasks;

public sealed class Loader {
    public Task<string> Read(bool cached) {
        if (cached) {
            return null;
        }

        return Task.FromResult("value");
    }
}
