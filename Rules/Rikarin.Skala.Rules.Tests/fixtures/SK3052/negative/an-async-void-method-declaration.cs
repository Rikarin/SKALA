using System.Threading.Tasks;

public sealed class Panel {
    public async void Refresh() {
        await Task.Yield();
    }
}
