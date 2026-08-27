using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Corpus.Safe;

/// <summary>
/// SK5007's twin: callbacks that are written for the same reason and actually decide something.
/// </summary>
public static class TrustDeliberately {
    const string Pinned = "9E99A48A9960B14926BB7F3B02E22DA2B0AB7280";

    public static HttpClient Pinning() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            errors == SslPolicyErrors.None
            || certificate?.GetCertHashString(HashAlgorithmName.SHA1) == Pinned;

        return new HttpClient(handler);
    }

    public static HttpClient StrictlyDefault() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, errors) => errors == SslPolicyErrors.None;
        return new HttpClient(handler);
    }

    public static HttpClient Refusing() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => false;
        return new HttpClient(handler);
    }

    public static SslStream ViaConstructorArgument(Stream inner) =>
        new SslStream(inner, false, (_, _, _, errors) => errors == SslPolicyErrors.None);

    /// <summary>⚠ Permissive, and still not reported: following a method group is out of scope.</summary>
    static bool Named(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors) =>
        errors == SslPolicyErrors.None;

    public static HttpClient ViaMethodGroup() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = Named;
        return new HttpClient(handler);
    }
}
