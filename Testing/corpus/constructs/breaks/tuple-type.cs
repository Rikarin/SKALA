using System.Collections.Generic;

namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). A tuple type is the one delimited list of the eleven the oracle never
// breaks at a comma — one too wide for its line breaks between an element's type and its name, or
// outside the type — so it has no plan, and what this file pins is that a break the author wrote
// inside one is kept on either side of the comma and after the `(`, with the next element one level
// in, the arrow after it broken, and a parameter list chopped around a multi-line parameter.
public class TupleType {
    (int a,
        int b) AfterComma() => default;

    (
        int a, int b) AfterOpen() => default;

    (int a
        , int b) BeforeComma() => default;

    (int a, int b) TupleExpressionTwin() => (1,
        2);

    void Local() {
        (int a,
            int b) t = (1, 2);
        List<(int a,
            int b)> list = null;
        var cast = ((int,
            int))t;
    }

    void Parameter((int a,
        int b) t) { }
}
