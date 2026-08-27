using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public static class Startup {
    public static void Configure() {
        ServicePointManager.ServerCertificateValidationCallback =
            delegate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors) {
                return true;
            };
    }
}
