using System.Threading.Tasks;

public sealed class Store {
    public async ValueTask<int> CountAsync() => await LoadAsync();

    static ValueTask<int> LoadAsync() => new(1);
}
