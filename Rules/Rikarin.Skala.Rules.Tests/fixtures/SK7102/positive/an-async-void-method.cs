using System.Threading.Tasks;

// `async void` returns nothing a caller can await or read, whatever the body does.
public sealed class Handler {
    /// <summary>Handles the click.</summary>
    /// <returns>A task that completes when the click is handled.</returns>
    public async void OnClick() {
        await Task.Yield();
    }
}
