// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// SK-DIV-0075, on its own file, and no option is globbed to it.
//
// ⚠ This fixture is expected to disagree with the oracle and exists to hold the disagreement still.
// `T x = default(T)` for a reference T has an initializer whose flow state is maybe-null while its
// declared type is not, so `var` would retype `x` as nullable; VarRule refuses on that ground and the
// oracle converts anyway. The value-type row beside it is the control — there the two agree — and the
// pair is what says the divergence is about nullability rather than about `default`.
public class VarAndMaybeNull {
    public void Locals() {
        var text = default(string);
        var list = default(List<int>);

        // The control: no maybe-null flow state, so both engines write `var`.
        var number = default(int);
        Console.WriteLine(text + list + number);
    }
}
