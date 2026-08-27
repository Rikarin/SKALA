using System.Threading.Tasks;

public sealed class View {
    public async void OnClicked() {
        await Task.Delay(1);
    }
}
