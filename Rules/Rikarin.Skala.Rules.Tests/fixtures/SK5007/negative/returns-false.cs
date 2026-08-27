using System.Net.Http;

public static class Client {
    public static HttpClient Make() {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => false;
        return new HttpClient(handler);
    }
}
