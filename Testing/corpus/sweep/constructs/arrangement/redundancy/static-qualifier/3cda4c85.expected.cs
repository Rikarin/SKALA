// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// resharper_static_members_qualify_members = none, resharper_static_members_qualify_with =
// declared_type. `none` is not "leave it alone": it is "qualify no member kind", so an existing
// type qualifier on a static member is removed.
public static class StaticQualifier {
    static int _shared;

    public static int Property { get; set; }

    // Removed: the receiver is the declaring type and the bare name binds to the same symbol.
    public static int ReadsField() => _shared;

    public static int ReadsProperty() => Property;

    public static int CallsMethod() => StaticQualifier.ReadsField();

    // ⚠ Not touched: `Console` is a different type, so `Console.WriteLine` is not a redundant
    // qualifier — it is the only way to name the member.
    public static void Other() {
        Console.WriteLine(_shared);
    }

    // ⚠ Not touched: a local shadows the field, so the bare name would bind to the local and the
    // qualifier is what makes the field reachable.
    public static int Shadowed() {
        var _shared = 5;
        return StaticQualifier._shared + _shared;
    }

    // ⚠ Not touched: `nameof` reads the spelling, and an unqualified name produces a different
    // string.
    public static string Named() => nameof(_shared);
}

public class InstanceReceiver {
    public int Value;

    // ⚠ Not touched: the receiver is an instance, not a type. Only a type receiver is a
    // static-member qualifier.
    public int Read(InstanceReceiver other) => other.Value;
}
