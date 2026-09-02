using System;

// ⚠ The reason this rule is narrow enough to ship. Skipping the right operand is what `&&`, `||`,
// `??` and `?:` are *for*, so a side effect there is deliberate far more often than not and nothing
// distinguishes the two cases. None of these four operators is examined.
public sealed class Gate {
    int attempts;

    public bool Guarded(string? text) => text != null && Consume(text);

    public bool Counted(bool flag) => flag && Bump();

    public string Built(string? text) => text ?? Build();

    public int Picked(bool flag) => flag ? attempts++ : 0;

    bool Bump() {
        attempts++;
        return true;
    }

    string Build() {
        attempts++;
        return "built";
    }

    static bool Consume(string text) => text.Length > 0;
}
