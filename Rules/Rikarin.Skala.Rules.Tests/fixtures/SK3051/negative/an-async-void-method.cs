using System.Threading.Tasks;

// The return type has to be a task; `async void` is SK3001's and SK3050's, and a token parameter
// is not what is wrong with it.
public sealed class Panel {
    public async void Refresh() {
        await Task.Delay(5);
    }
}
