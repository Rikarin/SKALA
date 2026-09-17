// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-17
using System;

namespace Constructs.Breaks;

// SK-DIV-0114 (issue #371). Several attributes in one section are filled: a kept break after a
// comma, before a comma, after the `[` or before the `]` comes back as written, a section past the
// margin fills at its commas, and the attributes after the first line up under the first attribute
// — one column past the bracket, or under `Obsolete` after a `return:` target — while after `[`↵
// they take the bracket's continuation level and a `]` on its own line sits on the owner's indent.
// A parameter or a type parameter leaves the section's line exactly when the section spans lines,
// kept or filled alike, and re-joins a break the author wrote after a one-line section's `]`; a
// single attribute whose arguments chop pushes the parameter down the same way. Before this file
// the section had no plan at all.
public class AttributeSection {
    [Obsolete,
     Serializable]
    void AfterComma() { }

    [
        Obsolete, Serializable]
    void AfterOpen() { }

    [Obsolete
     , Serializable]
    void BeforeComma() { }

    [Obsolete, Serializable
    ]
    void BeforeClose() { }

    [Obsolete("aaa", true), Serializable, CLSCompliant(true), Obsolete("bbb"), Serializable, CLSCompliant(false),
     Obsolete("ccc")]
    void TooLong() { }

    [Obsolete("a very long message that runs the line out past the margin of one hundred and twenty columns"),
     Serializable]
    void TooLongFirstAttribute() { }

    [Obsolete(
         "x",
         true
     ), Serializable]
    void ChoppedArgumentsInAPair() { }

    [Obsolete,
     Serializable]
    int field;

    [Obsolete,
     Serializable]
    int Property {
        [Obsolete,
         CLSCompliant(true)]
        get;
    }

    [return: Obsolete,
             CLSCompliant(true)]
    int ReturnTarget() => 0;

    void Parameter(
        [Obsolete,
         CLSCompliant(true)]
        int a
    ) { }

    void ParameterAfterOpen(
        [
            Obsolete, CLSCompliant(true)]
        int a
    ) { }

    void FilledParameter(
        [Obsolete("a long message that runs on and on"), Serializable, CLSCompliant(true), Obsolete("bbb"),
         Serializable, CLSCompliant(false)]
        int a
    ) { }

    void FittingParameterSection(
        [Obsolete("a long message that runs on"), Serializable, CLSCompliant(true), Obsolete("bbb")] int a
    ) { }

    void KeptAfterClose([Obsolete, Serializable] int a) { }

    void SingleKeptAfterClose([Obsolete] int a) { }

    void SingleChoppedArguments(
        [Obsolete(
            "x",
            true
        )]
        int a
    ) { }

    void SingleTooLongArguments(
        [Obsolete(
            "a very long message that runs the line out past the margin of one hundred and twenty columns",
            true
        )]
        int a
    ) { }

    void Lambda() {
        Func<int, int> f = (
            [Obsolete,
             CLSCompliant(true)]
            int a
        ) => a;
        Func<int, int> g = ([Obsolete] int a) => a;
    }
}

[Obsolete,
 Serializable]
public struct OnAType { }

[Obsolete, Serializable, CLSCompliant(true), Obsolete("bbb"), Serializable, CLSCompliant(false), Obsolete("ccc"),
 Serializable, CLSCompliant(true)]
public struct TooLongOnAType { }
