using System.Net.Http;
using System.Net.Security;

// More than one statement in the body: not provably constant, so not reported.
public static class Client {
    public static HttpClient Make() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, errors) => {
            var acceptable = errors == SslPolicyErrors.None;
            return acceptable;
        };

        return new HttpClient(handler);
    }
}
