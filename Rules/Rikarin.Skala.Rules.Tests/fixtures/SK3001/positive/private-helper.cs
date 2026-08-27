using System.Threading.Tasks;

public sealed class Cache {
    async void RefreshInBackground() {
        await Task.Yield();
    }

    public void Refresh() {
        RefreshInBackground();
    }
}
