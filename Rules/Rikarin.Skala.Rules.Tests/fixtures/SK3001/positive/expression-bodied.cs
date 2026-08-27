using System.Threading.Tasks;

public sealed class Pinger {
    public async void Ping() => await Task.Delay(1);
}
