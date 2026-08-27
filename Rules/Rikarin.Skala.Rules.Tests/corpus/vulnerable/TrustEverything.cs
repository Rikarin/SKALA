using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Corpus.Vulnerable;

/// <summary>SK5007 — four spellings, three of them on different APIs.</summary>
public static class TrustEverything {
    public static HttpClient ViaHandlerProperty() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        return new HttpClient(handler);
    }

    public static HttpClient ViaObjectInitializer() =>
        new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });

    public static void ViaServicePointManager() {
        ServicePointManager.ServerCertificateValidationCallback =
            delegate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors) {
                return true;
            };
    }

    public static SslStream ViaConstructorArgument(Stream inner) => new SslStream(inner, false, (_, _, _, _) => true);

    public static HttpClient ViaTheFrameworksOwnName() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        return new HttpClient(handler);
    }
}
