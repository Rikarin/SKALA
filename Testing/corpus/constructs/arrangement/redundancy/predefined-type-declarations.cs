using System;

namespace Skala.Corpus.Arrangement;

// dotnet_style_predefined_type_for_locals_parameters_members: a type in a declaration, as against
// redundancy/predefined-member-access.cs, which is the sibling key and the receiver of a member
// access.
//
// ⚠ Every governed position is written under its framework name, and that is the point rather than a
// style choice. `true` — the export — contracts all of them; `false` asks for the framework name and
// leaves the file as written. Written the other way round the file would measure the *expansion*,
// which the oracle performs and Skala has no rule for: see SK-DIV-0084. That is what
// `redundancy/qualifiers-and-parentheses.cs` did while this key was globbed to it — its bare
// `int _count;` and `void Parentheses(int a, int b, int c)` came back `Int32` from the oracle at
// `false`, so the key's whole row was about a construct it does not name.
public class PredefinedTypeDeclarations {
    Int32 _count;

    static readonly String Label = "x";

    public Boolean Enabled { get; set; }

    public Int64 Total { get; set; }

    public Double Ratio(Double numerator, Double denominator) {
        return numerator / denominator;
    }

    public String Describe(Object value, Char separator) {
        return Label + separator + value + _count + Enabled + Total;
    }

    // ⚠ Not touched: builtin_type_apply_to_native_integer = false, so the native integers keep the
    // framework spelling at either value.
    public IntPtr Handle { get; set; }

    // ⚠ Not touched: `nameof` reads the spelling, so rewriting it would change the string.
    public String Spelled => nameof(Int32);
}
