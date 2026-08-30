// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaCleanup generated=2026-08-30
namespace Skala.Corpus.Arrangement;

public delegate void Notify();

// The four dotnet_style_qualification_for_* keys, one member kind each, in both positions.
//
// ⚠ These are the keys the oracle reads for `this.`, and resharper_remove_this_qualifier is not.
// Measured against jb cleanupcode 2025.2.6 under the cleanup profile, one key at a time: with
// `dotnet_style_qualification_for_field = true` the bare `_value` becomes `this._value` and the
// other three kinds are untouched, and the same holds for property, method and event. With
// `resharper_remove_this_qualifier = false` and the four left at the export's `false`, the file
// comes back byte-identical — the qualifier is still removed. See SK-DIV-0070.
public class InstanceQualifier {
    int _value;

    int Number { get; set; }

    event Notify? Changed;

    public void Bare() {
        _value = 1;
        this.Number = 2;
        Helper();
        Changed += Nothing;
    }

    public void Qualified() {
        _value = 3;
        this.Number = 4;
        Helper();
        Changed += Nothing;
    }

    // ⚠ Not qualified in either direction: a parameter shadows the field, so the bare name binds to
    // the parameter and the qualifier is not redundant.
    public void Shadowed(int _value) {
        this._value = _value;
    }

    // ⚠ A static body has no `this`, so the adding direction has to decline it whatever the keys
    // say — and the removing direction never sees one here because none is legal.
    static void Nothing() {
        Shared++;
    }

    static int Shared { get; set; }

    void Helper() {
        _value++;
    }
}
