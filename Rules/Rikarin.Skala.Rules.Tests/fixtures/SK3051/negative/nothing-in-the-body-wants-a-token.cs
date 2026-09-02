using System.Threading.Tasks;

public sealed class Counter {
    public async Task<int> CountAsync(string text) {
        await Task.Yield();
        return text.Length;
    }
}
