// The declaration is disposed at the end of the enclosing block, which is where the statement's block
// ended: the order of the writes is pinned by Probe (#425).
public sealed class Resource : System.IDisposable {
    public static string Log = "";

    public void Dispose() => Log += "dispose;";
}

public static class Probe {
    static void Use() {
        Resource.Log += "before;";
        using (var resource = new Resource()) {
            Resource.Log += "body;";
        }
    }

    public static string Run() {
        Use();
        Resource.Log += "after;";
        return Resource.Log;
    }
}
