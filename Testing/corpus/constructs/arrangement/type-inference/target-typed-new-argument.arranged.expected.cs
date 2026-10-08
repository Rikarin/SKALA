// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaCleanup generated=2026-10-08
namespace Skala.Corpus.Arrangement;

// SK-DIV-0076 / #461, on its own file, and no option is globbed to it.
//
// An argument is a target-typed position: the oracle rewrites `Take(new object())` to `Take(new())`
// — but only while the call still reaches the same member. `Over` has two one-argument overloads, so
// `Over(new())` would be ambiguous; `Cross` shows the order-dependence, where the first argument
// alone keeps one overload and both together would not, and the oracle converts the first and stops.
// The declaration and assignment rows are the control: positions ObjectCreationRule always knew.
public class TargetTypedNewArgument {
    public void Take(object other) { }

    public void Named(int number, object other) { }

    public void Over(Uri value) { }

    public void Over(Version value) { }

    public void Cross(Uri first, Version second) { }

    public void Cross(Version first, Uri second) { }

    public void Generic<T>(T value) { }

    public void Arguments() {
        Take(new());
        Named(1, new());
        Over(new Uri("x:y"));
        Cross(new("x:y"), new Version(1, 0));
        Generic(new object());
        Generic<object>(new());
    }

    // The control: positions ObjectCreationRule already knows about.
    public void Controls() {
        var declared = new object();
        Held = new();
        Console.WriteLine(declared);
    }

    public object Held { get; set; }
}
