using System.Threading.Tasks;

// CS0231: an optional parameter cannot follow a `params` one, so there is no edit to offer.
public sealed class Sender {
    public async Task SendAllAsync(params string[] messages) {
        await Task.Delay(messages.Length);
    }
}
