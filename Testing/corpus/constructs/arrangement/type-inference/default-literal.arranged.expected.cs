// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaCleanup generated=2026-08-27
namespace Skala.Corpus.Arrangement;

// `default(T)` ⇒ `default` where the target type says which T, and not where it does not.
public class DefaultLiteral {
    int _field = default;

    public List<int> Property { get; set; } = default;

    public void Converted() {
        var number = default(int);
        var text = default(string);
        Held = default;
    }

    public void Refused() {
        // ⚠ An argument is never rewritten: `M(default)` may resolve to a different overload from
        // `M(default(int))`, and doc 06 asks for no ambiguity in overload resolution.
        Overloaded(default(int));
        Overloaded(default(string));

        // `var` has no type for the bare literal to take.
        var inferred = default(int);
    }

    public List<int> Held { get; set; }

    public void Overloaded(int value) { }

    public void Overloaded(string value) { }
}
