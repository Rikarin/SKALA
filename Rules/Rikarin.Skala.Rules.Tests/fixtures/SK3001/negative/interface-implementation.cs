using System.Threading.Tasks;

public interface IStartable {
    void Start();
}

public sealed class Worker : IStartable {
    public async void Start() {
        await Task.Delay(1);
    }
}
