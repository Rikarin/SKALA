using System.Net.Http;

public sealed class Warmer {
    public void Ping() {
        using (new HttpClient()) {
            // The client is owned by the `using` and nothing else; it dies here.
        }
    }
}
