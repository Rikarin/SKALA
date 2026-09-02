// A `!` that does real work, nowhere near an `is`.
class C {
    string M(string? text, object? value) {
        var length = text!.Length;
        var kind = value is string ? "string" : "other";
        return kind + length;
    }
}
