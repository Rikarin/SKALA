// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
using System;

namespace Constructs.Breaks;

// wrap_multiple_type_parameter_constraints_style and wrap_before_first_type_parameter_constraint.
// Every declaration here is written on one source line, so what the fixture pins is where the
// formatter puts breaks rather than which of the author's it kept.
public class TypeParameterConstraints {
    // Four clauses that do not fit together on the continuation line: chop_if_long gives each one
    // its own, chop_always the same, wrap_if_long fills them.
    public void FourLongConstraintsThatCannotFit<TFirst, TSecond, TThird, TFourth>(TFirst a)
        where TFirst : class, IDisposable
        where TSecond : struct, IComparable
        where TThird : notnull, ICloneable
        where TFourth : IDisposable, IEquatable<TFourth> { }

    // Two clauses that do fit once the first `where` has moved: the second question answers "yes"
    // and no clause takes a line of its own. This is the shape a single chop group gets wrong.
    public void TwoThatFitOnTheContinuationLine<TFirst, TSecond>(TFirst a)
        where TFirst : class where TSecond : struct, IComparable, ICloneable, IEquatable<TSecond> { }

    // Short enough to stay whole: nothing wraps at chop_if_long, and every clause takes a line at
    // chop_always. The shape that makes wrap_before_first_type_parameter_constraint decide, because
    // the declaration and its first clause fit together.
    public void Two<TFirst, TSecond>(TFirst a) where TFirst : class where TSecond : struct { }

    // A type declaration and a local function reach the same planning, on the same two questions.
    public void Local() {
        void Inner<TFirst, TSecond>(TFirst a)
            where TFirst : class, IDisposable, ICloneable, IEquatable<TFirst> where TSecond : struct, IComparable { }

        Inner<string, int>("a");
    }
}

public class GenericWithConstraints<TFirst, TSecond>
    where TFirst : class, IDisposable, ICloneable, IEquatable<TFirst> where TSecond : struct, IComparable {
    public TFirst? Value { get; set; }
}
