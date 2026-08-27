using System.Diagnostics;
using System.Net;

// One string that the child re-splits on its own rules, with the request's text inside it.
public static class Thumbnails {
    public static void Make(HttpListenerRequest request) {
        var start = new ProcessStartInfo("convert");
        start.Arguments = "-resize 100x100 " + request.QueryString["file"];
        Process.Start(start);
    }
}
