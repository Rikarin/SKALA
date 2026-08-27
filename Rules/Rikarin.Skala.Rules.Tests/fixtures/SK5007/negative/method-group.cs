using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

// ⚠ Following a method group is the inter-procedural analysis doc 08 puts out of scope. The rule
// says nothing rather than guessing, even though this particular one happens to be permissive.
public static class Client {
    static bool Validate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors
    ) =>
        true;

    public static HttpClient Make() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = Validate;
        return new HttpClient(handler);
    }
}
