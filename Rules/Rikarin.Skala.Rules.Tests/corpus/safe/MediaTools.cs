using System.Diagnostics;
using System.Net;

namespace Corpus.Safe;

/// <summary>SK5002's twin: `ArgumentList`, an allow-list, and a parsed integer.</summary>
public static class MediaTools {
    public static void Resize(HttpListenerRequest request) {
        var info = new ProcessStartInfo("convert");
        info.ArgumentList.Add("-resize");
        info.ArgumentList.Add("200x200");
        info.ArgumentList.Add(request.QueryString["source"]!);
        info.ArgumentList.Add("out.png");
        Process.Start(info);
    }

    public static void Probe(HttpListenerRequest request) {
        var info = new ProcessStartInfo("ffprobe");
        info.ArgumentList.Add("-i");
        info.ArgumentList.Add(request.QueryString["url"]!);
        Process.Start(info);
    }

    public static void Run(HttpListenerRequest request) {
        var tool = request.QueryString["tool"] switch {
            "resize" => "convert",
            "probe" => "ffprobe",
            _ => "true"
        };

        Process.Start(new ProcessStartInfo(tool, "--version"));
    }

    public static void ThroughABranch(HttpListenerRequest request, bool verbose) {
        var level = int.TryParse(request.QueryString["level"], out var parsed) ? parsed : 0;
        var arguments = verbose ? "-v " + level + " -i input.mp4" : "-i input.mp4";
        Process.Start("ffmpeg", arguments);
    }
}
