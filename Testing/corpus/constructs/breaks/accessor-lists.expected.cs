// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// An accessor list is on its owner's line or one accessor per line, and what decides it is what the
// accessors are, never where the author broke it (issues #416 and #417). The corpus held accessor
// lists written the way the oracle leaves them, so the sweep never saw that Skala kept each as
// written: a bodiless list broken over lines, which the oracle joins, and a one-line list with an
// expression-bodied or block-bodied accessor, which it expands. The margin expands a joined list
// rather than breaking inside the type, an initializer is not part of the measure, an accessor
// attribute on its own line breaks the list, and a block comment that ends a line joins with it
// while a line comment keeps the list expanded.

using System;
using System.Collections.Generic;

abstract class AccessorLists {
    int _value;

    int Expression {
        get => _value;
        set => _value = value;
    }

    int ExpressionGetter {
        get => 1;
    }

    int Block {
        get { return 1; }
    }

    int Mixed {
        get;
        set { }
    }

    int this[int index] {
        get => index;
        set { }
    }

    event EventHandler Changed {
        add { }
        remove { }
    }

    int Auto { get; set; }

    public int InnerLine { get; private set; }

    public required int OnTheBracesLine { get; init; }

    int WithInitializer { get; set; } = 1;

    public abstract string this[string key] { get; set; }

    int Attributed {
        [Obsolete]
        get;
        set;
    }

    [Obsolete]
    int AttributedHolder { get; set; }

    int BlockCommentEndsALine { get; /* c */ set; }

    int LineComment {
        get; // why
        set;
    }

    int LineCommentOnAJoinedList {
        get;
        set; // c
    }

    struct Readonly {
        public int Value { readonly get; set; }
    }

    interface IShape {
        int Sides { get; }
    }

    class Nested {
        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghiJKLMNO0123456789 { get; set; }

        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghiJKLMNOP0123456789 {
            get;
            set;
        }

        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz0123456789AbcdefghiJKLMNOQ0123456789X {
            get;
            set;
        }

        public Dictionary<string, List<int>> Abcdefghijklmnopqrstuvwxyz012 { get; set; } =
            new Dictionary<string, List<int>>();
    }
}
