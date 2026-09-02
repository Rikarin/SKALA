using System.Net.Http;
using System.Threading.Tasks;

// A client held in a field, and one handed back to the caller. Neither is ended by a `using`
// here, so neither is a lifetime this rule may speak about.
public sealed class Held {
    readonly HttpClient client = new();

    public HttpClient Borrow() {
        return client;
    }

    public static HttpClient Create() {
        var made = new HttpClient();
        return made;
    }

    public async Task<string> Fetch(string url) {
        return await client.GetStringAsync(url).ConfigureAwait(false);
    }
}
