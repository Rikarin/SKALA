using System.Diagnostics;
using System.Net;

// ⚠ The fix the rule recommends, and therefore a shape it must never report. Each element reaches
// the child verbatim, so there is no command line for a quote to break out of.
public static class Thumbnails {
    public static void Make(HttpListenerRequest request) {
        var start = new ProcessStartInfo("convert");
        start.ArgumentList.Add("-resize");
        start.ArgumentList.Add("100x100");
        start.ArgumentList.Add(request.QueryString["file"]!);
        Process.Start(start);
    }
}
