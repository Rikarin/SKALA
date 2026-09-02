// ⚠ The second legitimate look-alike, and the sharper one. `Value` over `inner` is a deliberate
// rename: `inner` backs no other property, so the two names were chosen rather than crossed.
sealed class Box {
    string inner = "";

    public string Value {
        get => inner;
        set => inner = value;
    }
}
