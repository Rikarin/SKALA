using System.Diagnostics;
using System.Net;

// An allow-list is the only correct answer for a tainted program name, and the rule sees that the
// value reaching `FileName` is a constant from the `switch` rather than the request's text.
public static class Tools {
    public static void Run(HttpListenerRequest request) {
        var name = request.QueryString["tool"] switch {
            "resize" => "convert",
            "probe" => "ffprobe",
            _ => "true"
        };

        Process.Start(new ProcessStartInfo(name));
    }
}
