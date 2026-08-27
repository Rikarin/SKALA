using System.Net.Http;
using System.Net.Security;

public static class Client {
    public static HttpClient Make() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback =
            (_, _, _, errors) => errors == SslPolicyErrors.None;

        return new HttpClient(handler);
    }
}
