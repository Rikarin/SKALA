using System.Threading.Tasks;

// CS0100: the fix writes `cancellationToken`, and the name is taken whatever its type is.
public sealed class Poller {
    public async Task PollAsync(int cancellationToken) {
        await Task.Delay(cancellationToken);
    }
}
