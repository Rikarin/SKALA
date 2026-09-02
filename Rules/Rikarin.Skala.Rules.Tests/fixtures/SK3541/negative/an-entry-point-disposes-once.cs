using System.Net.Http;
using System.Threading.Tasks;

// A client disposed once for the process is not a client disposed per call.
public static class Tool {
    public static async Task<int> Main(string[] args) {
        using var client = new HttpClient();
        foreach (var url in args) {
            System.Console.WriteLine(await client.GetStringAsync(url).ConfigureAwait(false));
        }

        return 0;
    }
}
