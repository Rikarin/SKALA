using System.Collections.Generic;

namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). A type argument list is filled exactly as a type parameter list is: a
// kept break after a comma, before a comma, after the `<` or before the `>` comes back as written
// with the next argument one level in and the arrow after it broken; a list past the margin fills
// at its commas; and it yields to everything around it — an `=` in front of it breaks first and the
// list fills only what still overflows on the continuation line, while a nested list keeps its head
// and breaks inside. Before this file the list had no plan at all, and the type parameter twin
// re-joined a kept `<`↵ and `>`↵ the oracle keeps.
public class TypeArgumentList {
    Dictionary<string,
        int> AfterComma() => null;

    Dictionary<
        string, int> AfterOpen() => null;

    Dictionary<string
        , int> BeforeComma() => null;

    Dictionary<string, int
        > BeforeClose() => null;

    Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>>>> Nested() => null;

    void Local() {
        var created = new Dictionary<string,
            int>();
        Generic<int,
            string>();
        List<Dictionary<string,
            int>> nested = null;
        Generic<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
        var wide = new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
        var result = Generic<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin, int>();
        var both = new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
        this.Field = new Dictionary<SomeVeryLongTypeNameNumberOneForTheMargin, SomeVeryLongTypeNameNumberTwoForTheMargin>();
        var deep = Generic<Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>>, int, int>();
    }

    object Field;

    T Generic<T, U, V>() => default;

    void TypeParameterTwinAfterComma<TFirst,
        TSecond>() { }

    void TypeParameterTwinAfterOpen<
        T, U>() { }

    void TypeParameterTwinBeforeClose<T, U
        >() { }
}

public class SomeVeryLongTypeNameNumberOneForTheMargin { }

public class SomeVeryLongTypeNameNumberTwoForTheMargin { }

public class Dictionary<TKey, TValue, TThird> { }
