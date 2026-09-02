// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// SK-DIV-0076, on its own file, and no option is globbed to it.
//
// ⚠ This fixture is expected to disagree with the oracle and exists to hold the disagreement still.
// An argument is a target-typed position: the oracle rewrites `Take(new object())` to `Take(new())`,
// and ObjectCreationRule's `TargetTypeOf` has no case for it, so Skala leaves the type name. The
// declaration and assignment rows beside it are the control — there the two agree — which is what
// says the gap is the argument position rather than the rule.
public class TargetTypedNewArgument {
    public void Take(object other) { }

    public void Named(int number, object other) { }

    public void Arguments() {
        Take(new());
        Named(1, new());
    }

    // The control: positions ObjectCreationRule already knows about.
    public void Controls() {
        var declared = new object();
        Held = new();
        Console.WriteLine(declared);
    }

    public object Held { get; set; }
}
