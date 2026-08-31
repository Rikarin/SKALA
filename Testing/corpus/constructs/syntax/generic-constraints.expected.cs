// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-31
using System;
using System.Collections.Generic;
using System.Numerics;

// AllowsConstraintClause, RefStructConstraint and DefaultConstraint occurred nowhere in the corpus,
// and TypeConstraint/StructConstraint/ClassConstraint were thin. A constraint clause is a
// Continuation, so it is `resharper_csharp_indent_type_constraints`,
// `wrap_before_first_type_parameter_constraint` and `wrap_before_type_parameter_constraint` that
// decide its layout — none of which is exercised by a constraint that fits on the declaration's line.
class GenericConstraints {
    // `allows ref struct` — C# 13. The clause is last by the language's own rule, so it is always the
    // one a chop lands on.
    static void Span<T>(T subject) where T : allows ref struct { }

    static void Both<T>(T subject) where T : struct, IEquatable<T>, allows ref struct { }

    static TResult Overflowing<TSubject, TAccumulator, TResult>(TSubject subject, TAccumulator seed)
        where TSubject : IComparable<TSubject>, IEquatable<TSubject>, ISpanFormattable, allows ref struct
        where TAccumulator : struct, INumber<TAccumulator>, IMinMaxValue<TAccumulator>
        where TResult : class, IReadOnlyCollection<TSubject>, new() =>
        new TResult();

    // Every constraint the language has, one per parameter, on a declaration wide enough to wrap.
    static void Everything<TClass, TStruct, TNew, TType, TNotNull, TUnmanaged>(
        TClass alpha,
        TStruct bravo,
        TNew charlie
    )
        where TClass : class?
        where TStruct : struct
        where TNew : new()
        where TType : IDisposable
        where TNotNull : notnull
        where TUnmanaged : unmanaged { }
}

abstract class DefaultConstraintBase {
    public abstract void Accept<T>(T? subject);
}

class DefaultConstraintDerived : DefaultConstraintBase {
    // `where T : default` — legal only on an override or explicit implementation, and the only place
    // the DefaultConstraint node can ever appear.
    public override void Accept<T>(T? subject) where T : default { }
}
