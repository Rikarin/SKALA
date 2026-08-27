using System.Net;

// A method called `Start` on a user's type is not `Process.Start`, and the rule resolves symbols
// rather than matching names.
public sealed class Pipeline {
    public void Start(string fileName, string arguments) { }

    public void Run(HttpListenerRequest request) {
        Start("convert", "-resize " + request.QueryString["size"]);
    }
}
