using System.Net.Http;
using System.Threading.Tasks;

public sealed class Reporter {
    public async Task<string> Send(string url) {
        using (var client = new HttpClient { Timeout = System.TimeSpan.FromSeconds(5) }) {
            return await client.GetStringAsync(url).ConfigureAwait(false);
        }
    }
}
