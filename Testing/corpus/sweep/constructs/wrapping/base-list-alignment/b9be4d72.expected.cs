// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
using System;

namespace Skala.Corpus.Wrapping;

// `align_multiline_extends_list`: the one member of the `align_multiline_*` family whose column is
// not the one its own node starts at. The base list's node starts at the `:`; the oracle aligns the
// wrapped base types to the *first base type*, two columns further right.
//
// The key is false in the export, so this file is the level-indented shape — one continuation indent
// from the declaration — and the option unit is what moves it onto the column.
//
// ⚠ The base list here wraps under the export's own 120-column margin. A shape that only wraps when
// a second key is flipped is one the per-option unit, which flips exactly one key, can never reach.
public class AlignedBaseList : System.Collections.Generic.IReadOnlyCollection<int>,
                               System.IDisposable,
                               System.ICloneable,
                               System.IFormattable {
    public int Count => 0;

    public System.Collections.Generic.IEnumerator<int> GetEnumerator() => throw new NotImplementedException();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        throw new NotImplementedException();

    public void Dispose() { }

    public object Clone() => this;

    public string ToString(string? format, IFormatProvider? formatProvider) => string.Empty;
}

// ⚠ A base list that fits stays where it is at either value: there is no break for a column to
// govern, and the key is about where a break lands rather than about whether one happens.
public class ShortBaseList : IDisposable {
    public void Dispose() { }
}
