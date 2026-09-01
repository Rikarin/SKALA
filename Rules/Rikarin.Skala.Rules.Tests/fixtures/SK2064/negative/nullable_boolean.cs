// ⚠ A separate exclusion with a separate reason. Lifted `&` and `|` on `bool?` are three-valued —
// `null & false` is `false`, not `null` — and `&&`/`||` cannot express that.
class C {
    bool? And(bool? a, bool? b) => a & b;

    bool? Or(bool? a, bool? b) => a | b;
}
