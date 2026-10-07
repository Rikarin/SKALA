// The issue's own shape (#386): the names sit in the signature two lines up and the reads ignore them.
public sealed class Inventory {
    public (int Count, string Name) Get() => (1, "a");

    public string Use() {
        var r = Get();
        return r.Item2 + ": " + r.Item1;
    }
}
