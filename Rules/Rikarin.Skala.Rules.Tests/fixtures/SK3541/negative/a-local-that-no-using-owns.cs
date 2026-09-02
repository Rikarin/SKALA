using System.Net.Http;
using System.Threading.Tasks;

// Not disposed at all. Whether that is a leak is `SK3501`'s question and the answer for this
// type is "no" — which is exactly why this rule reports the `using` and not its absence.
public sealed class Loose {
    public async Task<string> Fetch(string url) {
        var client = new HttpClient();
        return await client.GetStringAsync(url).ConfigureAwait(false);
    }
}
