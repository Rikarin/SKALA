// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
using System.Collections.Generic;

// The `indent_*_pars` and `indent_*_angles` family. Each key sets two numbers — how many levels the
// contents take, and how many the closing delimiter takes — from the line the opening delimiter is
// on. At the export's `inside` that is one and zero.
//
// Two shapes are needed and only one of them is this export's own. A chopped call and a chopped
// parameter list reach a closing delimiter on a line of its own because the wrap keys put it there;
// a bracket, a type argument list and a type parameter list never do, and the probes that reported
// `indent_pars`, `indent_typearg_angles` and `indent_typeparam_angles` inert had no input where the
// delimiter they govern lands on a line at all. `keep_user_linebreaks = true` supplies one: a
// delimiter the *author* left on its own line is kept there.
class DelimiterIndent {
    void ChoppedInvocation() {
        Consume(
            FirstArgumentValueNameHere,
            SecondArgumentValueNameHere,
            ThirdArgumentValueNameHere,
            FifthArgumentValueNameHere,
            FourthArgumentValue
            );
    }

    void ChoppedDeclaration(
        int firstParameterNameHere,
        int secondParameterNameHere,
        int thirdParameterNameHere,
        int fourthParameterNameHere
    ) { }

    void TypeArgumentAngleTheAuthorPutOnItsOwnLine() {
        var value = new Dictionary<FirstTypeArgumentNameHere, SecondTypeArgumentNameHere
        >();
    }

    void ElementAccessBracketTheAuthorPutOnItsOwnLine() {
        var value = TheArrayVariableName[FirstIndexValueName + SecondIndexValueName
        ];
    }

    // ⚠ A tuple's or a grouping parenthesis's stray `)` is deliberately not here. SK-DIV-0041: the
    // oracle puts it one level in and Skala aligns it with its opener's line, and it is not this
    // family's doing — all four values of `indent_pars` return that `)` at the same column. A
    // fixture named for a family must not pin a divergence the family is inert under.
}

// ⚠ A type parameter list's `>` on a line of its own is deliberately not here, and for a different
// reason from the tuple's: the oracle keeps the author's break before it and Skala rejoins it, so
// the shape never survives to the point where `indent_typeparam_angles` could move the `>`. That is
// a break-preservation disagreement in PlanTypeParameters, not an indentation one — SK-DIV-0042.
// The key's *contents* half is reachable, and `constructs/breaks/type-parameter-list.cs` is where it
// already lives — a list wide enough that the oracle fills it at its own commas — so that is the
// fixture the key's `oracle` glob names rather than a second copy here.

class PrimaryConstructorParameters(
    int firstParameterNameHere,
    int secondParameterNameHere,
    int thirdParameterNames,
    int fourthParameterNames) { }
