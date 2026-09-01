public sealed record Request(string Path, string Method, int Timeout);

public sealed class Sender {
    public string Send(Request request, string method, int timeout) =>
        Describe(new Request(request.Path, method, timeout));

    static string Describe(Request request) => request.Path;
}
