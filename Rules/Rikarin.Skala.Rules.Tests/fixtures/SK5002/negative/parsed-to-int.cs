using System.Diagnostics;
using System.Net;

public static class Renice {
    public static void Run(HttpListenerRequest request) {
        var priority = int.Parse(request.QueryString["priority"]!);
        Process.Start("nice", "-n " + priority + " work.sh");
    }
}
