using System;

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
        Take(new object());
        Named(1, other: new object());
        Over(new Uri("x:y"));
        Cross(new Uri("x:y"), new Version(1, 0));
        Generic(new object());
        Generic<object>(new object());
    }

    // The control: positions ObjectCreationRule already knows about.
    public void Controls() {
        object declared = new object();
        Held = new object();
        Console.WriteLine(declared);
    }

    public object Held { get; set; }
}
