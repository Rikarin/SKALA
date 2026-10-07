// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
using System;

abstract class AccessorLists {
    int _value;

    int AutoOnOneLine { get; set; }

    int AutoBroken {
        get;
        set;
    }

    int AutoInnerLine {
        get;
        private set;
    }

    int AutoOnTheBracesLine {
        get;
        init;
    }

    int ExpressionOnOneLine { get => _value; set => _value = value; }

    int ExpressionBroken {
        get => _value;
        set => _value = value;
    }

    int BlockOnOneLine { get { return _value; } }

    int MixedOnOneLine { get; set { } }

    public abstract int this[int index] {
        get;
        set;
    }

    event EventHandler Changed { add { } remove { } }

    class Nested {
        int AutoBroken {
            get;
            set;
        }

        int ExpressionOnOneLine { get => 1; }
    }
}
