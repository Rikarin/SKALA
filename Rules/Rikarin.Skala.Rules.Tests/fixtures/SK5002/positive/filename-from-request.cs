using System.Diagnostics;
using System.Net;

// Worse than an argument: the request decides which program runs.
public static class Plugins {
    public static void Invoke(HttpListenerRequest request) {
        var start = new ProcessStartInfo();
        start.FileName = request.QueryString["tool"]!;
        Process.Start(start);
    }
}
