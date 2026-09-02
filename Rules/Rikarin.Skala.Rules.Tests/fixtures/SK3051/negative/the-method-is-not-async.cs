using System.Threading.Tasks;

public sealed class Poller {
    public Task PollAsync() => Task.Delay(50);
}
