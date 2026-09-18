using System;
using System.Collections.Generic;
using System.Text;

namespace Constructs.Breaks;

// Issue #377. A type argument list whose earlier argument holds another list — a tuple type around
// nested generics, or nested generics alone — breaks at its own comma and leaves every nested list
// whole: the list's points yield to what precedes the list and to nothing inside it. Before this file
// the nested `List<Guid>` in the first argument measured the whole line, broke at its own `<`, and the
// outer comma broke as well. The attributed shapes are the ones the oracle answers the same on both of
// its passes: a section that stays alone because the parameter's first line does not fit beside it,
// whole or filled, and the joined form it returns unchanged. The flat attributed parameter whose first
// line would fit beside the section is SK-DIV-0119 and is deliberately not here.
public class NestedListInATypeArgument {
    public static void Parameter(Dictionary<string, (char First, StringBuilder Second)> p13, ValueTask<int> p14, Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second), List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> p15) { }

    public static void NoTuple(Dictionary<string, (char First, StringBuilder Second)> p13, ValueTask<int> p14, Dictionary<Dictionary<SomeVeryLongTypeNameNumberOne, List<SomeVeryLongTypeNameNumberTwo>>, List<Dictionary<SomeVeryLongTypeNameNumberOne, SomeVeryLongTypeNameNumberTwo>>> p15) { }

    public static void SectionAloneWhole(Dictionary<string, (char First, StringBuilder Second)> p13, ValueTask<int> p14, [Obsolete] Dictionary<SomeVeryLongTypeNameNumberOne, SomeVeryLongTypeNameNumberTwo, SomeVeryLongTypeNameNumberTwo> p15) { }

    public static void SectionAloneFilled(Dictionary<string, (char First, StringBuilder Second)> p13, ValueTask<int> p14, [Obsolete] Dictionary<SomeVeryLongTypeNameNumberOne, SomeVeryLongTypeNameNumberTwo, SomeVeryLongTypeNameNumberTwo, SomeVeryLongTypeNameNumberTwo> p15) { }

    public static void SectionJoined(
        Dictionary<string, (char First, StringBuilder Second)> p13,
        ValueTask<int> p14,
        [Obsolete] Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second),
            List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> p15
    ) { }

    public static void Local() {
        Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second), List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>> local = null;
        var created = new Dictionary<(Dictionary<Guid, List<Guid>> First, StringBuilder Second), List<(Dictionary<Guid, List<Guid>> First, StringBuilder Second)>>();
    }
}

public class SomeVeryLongTypeNameNumberOne { }

public class SomeVeryLongTypeNameNumberTwo { }

public class Dictionary<TKey, TValue, TThird> { }

public class Dictionary<TKey, TValue, TThird, TFourth> { }

public struct ValueTask<T> { }
