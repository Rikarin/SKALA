using System.Diagnostics;
using System.Net;

public static class Archive {
    public static void Extract(HttpListenerRequest request) {
        Process.Start("tar", "-xf " + request.QueryString["name"]);
    }
}
