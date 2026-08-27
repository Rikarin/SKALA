using System.Diagnostics;
using System.Net;

public static class Version {
    public static void Print(HttpListenerRequest request) {
        Process.Start("git", "--version");
    }
}
