using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #441, SK-DIV-0202: with <c>remove_blank_lines_near_braces_*</c> off, a member's blank-line
///     requirement and <c>blank_lines_after_block_statements</c> are not paid against the body's own brace;
///     a using list's and a region's are. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own
///     output for the input, and each test asserts the second pass too.
/// </summary>
public sealed class NearBraceRequirementIssue441Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     A multi-line first and last method, a nested type, a type in a namespace, a property, glued comments, at
    ///     <c>remove_blank_lines_near_braces_in_declarations = false</c>.
    /// </summary>
    [Fact]
    public void Declarations_AMemberRequirementIsNotPaidAgainstTheTypesBrace() =>
        Agrees(
            """
            class K {
                void M() {
                }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) {
                    }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() {
                    }
                }
            }

            namespace N {
                class X {
                    void E() {
                    }
                }
            }

            struct S {
                int F;
                void G() {
                }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() {
                }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }
                    Foo(() => {
                        if (a) {
                            x();
                        }
                    });
                }
            }
            """,
            """
            class K {
                void M() { }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) { }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() { }
                }
            }

            namespace N {
                class X {
                    void E() { }
                }
            }

            struct S {
                int F;
                void G() { }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() { }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }

                    Foo(() => {
                            if (a) {
                                x();
                            }
                        }
                    );
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_declarations", "false")
        );

    /// <summary>
    ///     An <c>if</c>, a <c>while</c> and a <c>switch</c> block ending a method, a getter and a lambda, at
    ///     <c>remove_blank_lines_near_braces_in_code = false</c>.
    /// </summary>
    [Fact]
    public void Code_AfterBlockStatementsIsNotPaidBeforeTheBodysBrace() =>
        Agrees(
            """
            class K {
                void M() {
                }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) {
                    }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() {
                    }
                }
            }

            namespace N {
                class X {
                    void E() {
                    }
                }
            }

            struct S {
                int F;
                void G() {
                }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() {
                }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }
                    Foo(() => {
                        if (a) {
                            x();
                        }
                    });
                }
            }
            """,
            """
            class K {
                void M() { }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) { }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() { }
                }
            }

            namespace N {
                class X {
                    void E() { }
                }
            }

            struct S {
                int F;
                void G() { }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() { }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }

                    Foo(() => {
                            if (a) {
                                x();
                            }
                        }
                    );
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>The same input at the export, where the removal had hidden it.</summary>
    [Fact]
    public void TheExport_IsUnchanged() =>
        Agrees(
            """
            class K {
                void M() {
                }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) {
                    }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() {
                    }
                }
            }

            namespace N {
                class X {
                    void E() {
                    }
                }
            }

            struct S {
                int F;
                void G() {
                }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() {
                }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }
                    Foo(() => {
                        if (a) {
                            x();
                        }
                    });
                }
            }
            """,
            """
            class K {
                void M() { }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) { }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() { }
                }
            }

            namespace N {
                class X {
                    void E() { }
                }
            }

            struct S {
                int F;
                void G() { }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() { }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }

                    Foo(() => {
                            if (a) {
                                x();
                            }
                        }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     With both removals off: a using list's and a region's requirements are paid next to a brace; a member's and a
    ///     block statement's are not.
    /// </summary>
    [Fact]
    public void BoundaryRequirements_ArePaidNextToABrace() =>
        Agrees(
            """
            namespace N {
                using System;
            }

            namespace N2 {
                #region R
                class A {
                }
                #endregion
            }

            class B {
                #region R
                void M() {
                }
                #endregion
            }

            class C {
                // first
                int f;
                // last
            }

            class D {
                void M() {
                    // first
                    x();
                    // last
                }

                void N() {
                    return;
                }

                void O() {
                    x();
                    return;
                }

                void P() {
                    Foo(a,
                        b);
                }

                void Q() {
                    if (a) {
                    }
                    x();
                }

                void R() {
                    void Local() {
                        x();
                    }
                }

                void S() {
                    switch (a) {
                        case 1:
                            x();
                            break;
                    }
                }

                void T() {
                    {
                        x();
                    }
                }

                int F = Foo(a,
                    b);
            }

            class E {
                int F = Foo(a,
                    b);
            }
            """,
            """
            namespace N {
                using System;

            }

            namespace N2 {

                #region R

                class A { }

                #endregion

            }

            class B {

                #region R

                void M() { }

                #endregion

            }

            class C {
                // first
                int f;
                // last
            }

            class D {
                void M() {
                    // first
                    x();
                    // last
                }

                void N() {
                    return;
                }

                void O() {
                    x();
                    return;
                }

                void P() {
                    Foo(
                        a,
                        b
                    );
                }

                void Q() {
                    if (a) { }

                    x();
                }

                void R() {
                    void Local() {
                        x();
                    }
                }

                void S() {
                    switch (a) {
                        case 1:
                            x();
                            break;
                    }
                }

                void T() {
                    {
                        x();
                    }
                }

                int F = Foo(
                    a,
                    b
                );
            }

            class E {
                int F = Foo(
                    a,
                    b
                );
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_declarations", "false"),
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>The author's blank lines next to every brace are kept, capped, at <c>..._in_declarations = false</c>.</summary>
    [Fact]
    public void AnAuthorsBlank_IsKeptNextToTheBrace_Declarations() =>
        Agrees(
            """
            class K {

                void M() {


                }

            }

            class L {

                void A() {

                    if (a) {

                        x();

                    }

                }

                void B() {

                    x();
                    if (a) {

                        y();

                    }

                }

                void C() {

                    while (a) {


                    }

                }

                int P {

                    get {

                        if (a) {

                            return 1;

                        }

                    }

                }

                class Inner {

                    void D() {


                    }

                }

            }

            namespace N {

                class X {

                    void E() {


                    }

                }

            }

            struct S {

                int F;
                void G() {


                }

            }

            class T {

                int f;

            }

            class U {

                // c
                void H() {


                }
                // d

            }

            class V {

                void I() {

                    switch (a) {

                        case 1: {

                            break;

                        }

                    }
                    Foo(() => {

                        if (a) {

                            x();

                        }

                    });

                }

            }
            """,
            """
            class K {

                void M() { }

            }

            class L {

                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) { }
                }

                int P {

                    get {
                        if (a) {
                            return 1;
                        }
                    }

                }

                class Inner {

                    void D() { }

                }

            }

            namespace N {

                class X {

                    void E() { }

                }

            }

            struct S {

                int F;
                void G() { }

            }

            class T {

                int f;

            }

            class U {

                // c
                void H() { }
                // d

            }

            class V {

                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }

                    Foo(() => {
                            if (a) {
                                x();
                            }
                        }
                    );
                }

            }
            """,
            ("skala_remove_blank_lines_near_braces_in_declarations", "false")
        );

    /// <summary>The same at <c>..._in_code = false</c>.</summary>
    [Fact]
    public void AnAuthorsBlank_IsKeptNextToTheBrace_Code() =>
        Agrees(
            """
            class K {

                void M() {


                }

            }

            class L {

                void A() {

                    if (a) {

                        x();

                    }

                }

                void B() {

                    x();
                    if (a) {

                        y();

                    }

                }

                void C() {

                    while (a) {


                    }

                }

                int P {

                    get {

                        if (a) {

                            return 1;

                        }

                    }

                }

                class Inner {

                    void D() {


                    }

                }

            }

            namespace N {

                class X {

                    void E() {


                    }

                }

            }

            struct S {

                int F;
                void G() {


                }

            }

            class T {

                int f;

            }

            class U {

                // c
                void H() {


                }
                // d

            }

            class V {

                void I() {

                    switch (a) {

                        case 1: {

                            break;

                        }

                    }
                    Foo(() => {

                        if (a) {

                            x();

                        }

                    });

                }

            }
            """,
            """
            class K {
                void M() { }
            }

            class L {
                void A() {

                    if (a) {

                        x();

                    }

                }

                void B() {

                    x();
                    if (a) {

                        y();

                    }

                }

                void C() {

                    while (a) { }

                }

                int P {
                    get {

                        if (a) {

                            return 1;

                        }

                    }
                }

                class Inner {
                    void D() { }
                }
            }

            namespace N {
                class X {
                    void E() { }
                }
            }

            struct S {
                int F;
                void G() { }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() { }
                // d
            }

            class V {
                void I() {

                    switch (a) {

                        case 1: {

                            break;

                        }

                    }

                    Foo(() => {

                            if (a) {

                                x();

                            }

                        }
                    );

                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_code", "false")
        );

    /// <summary>Both removals off and both keep keys at 1.</summary>
    [Fact]
    public void AnAuthorsBlank_IsKeptNextToTheBrace_BothAtACapOfOne() =>
        Agrees(
            """
            class K {

                void M() {


                }

            }

            class L {

                void A() {

                    if (a) {

                        x();

                    }

                }

                void B() {

                    x();
                    if (a) {

                        y();

                    }

                }

                void C() {

                    while (a) {


                    }

                }

                int P {

                    get {

                        if (a) {

                            return 1;

                        }

                    }

                }

                class Inner {

                    void D() {


                    }

                }

            }

            namespace N {

                class X {

                    void E() {


                    }

                }

            }

            struct S {

                int F;
                void G() {


                }

            }

            class T {

                int f;

            }

            class U {

                // c
                void H() {


                }
                // d

            }

            class V {

                void I() {

                    switch (a) {

                        case 1: {

                            break;

                        }

                    }
                    Foo(() => {

                        if (a) {

                            x();

                        }

                    });

                }

            }
            """,
            """
            class K {

                void M() { }

            }

            class L {

                void A() {

                    if (a) {

                        x();

                    }

                }

                void B() {

                    x();
                    if (a) {

                        y();

                    }

                }

                void C() {

                    while (a) { }

                }

                int P {

                    get {

                        if (a) {

                            return 1;

                        }

                    }

                }

                class Inner {

                    void D() { }

                }

            }

            namespace N {

                class X {

                    void E() { }

                }

            }

            struct S {

                int F;
                void G() { }

            }

            class T {

                int f;

            }

            class U {

                // c
                void H() { }
                // d

            }

            class V {

                void I() {

                    switch (a) {

                        case 1: {

                            break;

                        }

                    }

                    Foo(() => {

                            if (a) {

                                x();

                            }

                        }
                    );

                }

            }
            """,
            ("skala_remove_blank_lines_near_braces_in_declarations", "false"),
            ("skala_remove_blank_lines_near_braces_in_code", "false"),
            ("skala_keep_blank_lines_in_code", "1"),
            ("skala_keep_blank_lines_in_declarations", "1")
        );

    /// <summary>The same input at the export, which removes them.</summary>
    [Fact]
    public void AnAuthorsBlank_NextToTheBrace_TheExport() =>
        Agrees(
            """
            class K {

                void M() {


                }

            }

            class L {

                void A() {

                    if (a) {

                        x();

                    }

                }

                void B() {

                    x();
                    if (a) {

                        y();

                    }

                }

                void C() {

                    while (a) {


                    }

                }

                int P {

                    get {

                        if (a) {

                            return 1;

                        }

                    }

                }

                class Inner {

                    void D() {


                    }

                }

            }

            namespace N {

                class X {

                    void E() {


                    }

                }

            }

            struct S {

                int F;
                void G() {


                }

            }

            class T {

                int f;

            }

            class U {

                // c
                void H() {


                }
                // d

            }

            class V {

                void I() {

                    switch (a) {

                        case 1: {

                            break;

                        }

                    }
                    Foo(() => {

                        if (a) {

                            x();

                        }

                    });

                }

            }
            """,
            """
            class K {
                void M() { }
            }

            class L {
                void A() {
                    if (a) {
                        x();
                    }
                }

                void B() {
                    x();
                    if (a) {
                        y();
                    }
                }

                void C() {
                    while (a) { }
                }

                int P {
                    get {
                        if (a) {
                            return 1;
                        }
                    }
                }

                class Inner {
                    void D() { }
                }
            }

            namespace N {
                class X {
                    void E() { }
                }
            }

            struct S {
                int F;
                void G() { }
            }

            class T {
                int f;
            }

            class U {
                // c
                void H() { }
                // d
            }

            class V {
                void I() {
                    switch (a) {
                        case 1: {
                            break;
                        }
                    }

                    Foo(() => {
                            if (a) {
                                x();
                            }
                        }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     With both removals off, <c>blank_lines_before_control_transfer_statements</c> and
    ///     <c>blank_lines_before_single_line_comment</c> at 1 are still paid between two statements.
    /// </summary>
    [Fact]
    public void StatementRequirements_ArePaidInsideTheBody() =>
        Agrees(
            """
            class D {
                void O() {
                    x();
                    return;
                }

                void M() {
                    x();
                    // last
                    y();
                }
            }
            """,
            """
            class D {
                void O() {
                    x();

                    return;
                }

                void M() {
                    x();

                    // last
                    y();
                }
            }
            """,
            ("skala_remove_blank_lines_near_braces_in_declarations", "false"),
            ("skala_remove_blank_lines_near_braces_in_code", "false"),
            ("skala_blank_lines_before_control_transfer_statements", "1"),
            ("skala_blank_lines_before_single_line_comment", "1")
        );
}
