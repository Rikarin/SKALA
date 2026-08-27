using System;
using System.Collections.Generic;

namespace Skala.Corpus.Arrangement;

// `default(T)` ⇒ `default` where the target type says which T, and not where it does not.
public class DefaultLiteral {
    private int _field = default(int);

    public List<int> Property { get; set; } = default(List<int>);

    public void Converted() {
        int number = default(int);
        string text = default(string);
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

    // ⚠ A parameter's own default is the one position `default_value_when_type_NOT_evident` governs:
    // the reader cannot see the type from the initialiser, only from the parameter beside it.
    public void WithDefaults(int count = default(int), string label = default(string)) {
        Console.WriteLine(count + label);
    }

    public void Overloaded(int value) {
    }

    public void Overloaded(string value) {
    }
}
