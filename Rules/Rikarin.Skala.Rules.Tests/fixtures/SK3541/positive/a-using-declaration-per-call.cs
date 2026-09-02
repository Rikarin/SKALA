using System.Net.Http;
using System.Threading.Tasks;

public static class Downloader {
    public static async Task<string> Fetch(string url) {
        using var client = new HttpClient();
        return await client.GetStringAsync(url).ConfigureAwait(false);
    }
}
