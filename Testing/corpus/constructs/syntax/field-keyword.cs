using System;
using System.Collections.Generic;

// FieldExpression — the C# 14 `field` contextual keyword — occurred nowhere in the corpus. It is a
// primary expression inside an accessor, so it takes part in every wrapping decision an identifier
// does, and the accessor bodies below are sized so that some of those decisions have to be made.
class FieldKeyword {
    public string Trimmed {
        get => field;
        set => field = value?.Trim() ?? string.Empty;
    }

    public int Counted {
        get { return field; }
        set { field = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value; }
    }

    public IReadOnlyList<string> Names {
        get => field ??= new List<string> { "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf" };
        set;
    }

    public string Overflowing {
        get => field ?? throw new InvalidOperationException("the overflowing property was read before anything assigned it");
        set => field = value is { Length: > 0 } supplied ? supplied : throw new ArgumentException("empty", nameof(value));
    }

    // `field` in a getter that is not a simple return: the keyword binds inside a nested lambda too.
    public Func<string> Deferred {
        get => () => field() + "/" + field();
        init => field = value;
    }

    public required int Required { get => field; init => field = value; }
}
