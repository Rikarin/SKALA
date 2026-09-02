using System.Net.Http;
using System.Threading.Tasks;

// One client for the process. It is never a `using` resource, so it never reaches the rule.
public static class Api {
    static readonly HttpClient Client = new();

    public static async Task<string> Fetch(string url) {
        return await Client.GetStringAsync(url).ConfigureAwait(false);
    }
}
