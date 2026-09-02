using System;

// Shape C, second half: `ProcessExit` is an *instance* event, and it is reached through
// `AppDomain.CurrentDomain`, a static property. That is what makes it outlive the object exactly as
// a static event would — the receiver is the process, and nothing shorter-lived is holding it.
public sealed class Journal {
    readonly string path;

    public Journal(string path) {
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        this.path = path;
    }

    public string Path => path;

    void OnExit(object? sender, EventArgs e) => Console.WriteLine(path);
}
