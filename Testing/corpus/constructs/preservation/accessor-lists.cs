using System;

abstract class AccessorLists {
    int _value;

    int AutoOnOneLine { get; set; }

    int AutoBroken {
        get;
        set;
    }

    int AutoInnerLine {
        get; private set;
    }

    int AutoOnTheBracesLine { get;
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
