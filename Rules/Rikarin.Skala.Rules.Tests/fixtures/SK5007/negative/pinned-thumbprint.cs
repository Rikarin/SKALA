using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;

// The one legitimate reason to write a callback at all, and it must not be reported.
public static class Client {
    const string Pinned = "9E99A48A9960B14926BB7F3B02E22DA2B0AB7280";

    public static HttpClient Make() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            errors == SslPolicyErrors.None
            || certificate?.GetCertHashString(HashAlgorithmName.SHA1) == Pinned;

        return new HttpClient(handler);
    }
}
