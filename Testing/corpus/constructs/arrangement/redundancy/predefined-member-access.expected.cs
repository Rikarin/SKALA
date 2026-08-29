// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
using System;

namespace Skala.Corpus.Arrangement;

// dotnet_style_predefined_type_for_member_access = true, which is a *different* key from
// dotnet_style_predefined_type_for_locals_parameters_members and governs a different position: the
// receiver of a member access rather than a type in a declaration.
//
// ⚠ docs/plan/17 found this key at Tier D while the behaviour it names was already shipping — the
// rewrite read the declaration key and applied it to both positions, so this key could not be
// observed through its own value. This fixture is what makes it observable.
public class PredefinedMemberAccess {
    // The receiver: governed by ..._for_member_access.
    public int Max => Int32.MaxValue;

    public long Min => Int64.MinValue;

    public string Empty => String.Empty;

    public bool Parsed => Boolean.Parse("true");

    public int Rounded => (int)Double.Floor(1.5);

    // ⚠ Not touched: builtin_type_apply_to_native_integer = false, so the native integers keep the
    // framework spelling in either position.
    public IntPtr Handle => IntPtr.Zero;

    // ⚠ Not touched: `nameof` reads the spelling, so rewriting the receiver changes the string.
    public string Spelled => nameof(Int32);
}
