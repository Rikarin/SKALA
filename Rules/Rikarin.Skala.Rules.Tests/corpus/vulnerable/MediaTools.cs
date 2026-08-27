using System.Diagnostics;
using System.Net;

namespace Corpus.Vulnerable;

/// <summary>SK5002 — the request choosing a command line, and then a program.</summary>
public static class MediaTools {
    public static void Resize(HttpListenerRequest request) {
        var info = new ProcessStartInfo("convert");
        info.Arguments = "-resize 200x200 " + request.QueryString["source"] + " out.png";
        Process.Start(info);
    }

    public static void Probe(HttpListenerRequest request) {
        Process.Start("ffprobe", "-i " + request.QueryString["url"]);
    }

    public static void Run(HttpListenerRequest request) {
        Process.Start(new ProcessStartInfo(request.QueryString["tool"]!, "--version"));
    }

    public static void ThroughABranch(HttpListenerRequest request, bool verbose) {
        var arguments = "-i input.mp4";
        if (verbose) {
            arguments = "-v " + request.QueryString["level"] + " -i input.mp4";
        }

        Process.Start("ffmpeg", arguments);
    }
}
