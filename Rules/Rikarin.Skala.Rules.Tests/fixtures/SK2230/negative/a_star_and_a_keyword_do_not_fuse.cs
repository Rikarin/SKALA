// ⚠ `"select *" + "from t"` is `select *from t`, which every SQL tokenizer accepts: `*` cannot be
// part of the same token as `f`. The defect needs two *word* characters to meet.
public sealed class Queries {
    public string All() =>
        "select *"
        + "from t";
}
