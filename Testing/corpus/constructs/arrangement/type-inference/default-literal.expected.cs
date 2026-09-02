// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
using System;
using System.Collections.Generic;

namespace Skala.Corpus.Arrangement;

// `default(T)` ⇒ `default` where the target type says which T, and not where it does not.
public class DefaultLiteral {
    private int _field = default(int);

    public List<int> Property { get; set; } = default(List<int>);

    // ⚠ No `string text = default(string);` here, and the reason is SK-DIV-0075 rather than taste: a
    // local whose initializer is `default(T)` for a *reference* T is a `var` candidate the oracle takes
    // and Skala's nullable-flow precondition refuses, so the two disagree on that line before any key
    // is touched — and both of this file's keys then attribute nothing. The construct is pinned on
    // var-and-maybe-null.cs, which nothing is globbed to.
    public void Converted() {
        int number = default(int);
        Held = default(List<int>);
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

    // ⚠ A parameter's own default is EVIDENT — the type is written on the parameter beside it — so
    // `default_value_when_type_evident` governs this line and not its sibling. The comment here said
    // the opposite until the key-flip sweep measured it: flipped one at a time, the two keys' rows
    // came back exact mirror images, with `_evident = default_expression` expanding these parameter
    // defaults and leaving `Held = default` alone, and `_not_evident = default_expression` doing the
    // reverse. `Held` is the not-evident position: it is an assignment to a name declared elsewhere.
    public void WithDefaults(int count = default(int), string label = default(string)) {
        Console.WriteLine(count + label);
    }

    public void Overloaded(int value) { }

    public void Overloaded(string value) { }
}
