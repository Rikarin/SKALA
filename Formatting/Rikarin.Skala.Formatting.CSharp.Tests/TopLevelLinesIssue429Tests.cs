namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #429: a block comment before a top-level declaration on the same line was kept there, and
///     the oracle breaks after it. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output for
///     the input, and <see cref="Oracle.Agrees" /> asserts the second pass too.
/// </summary>
public sealed class TopLevelLinesIssue429Tests {
    /// <summary>
    ///     The issue's shape and its family: a <c>/* */</c> or <c>/** */</c> before a <c>using</c>, an
    ///     <c>[assembly: …]</c> list, a type, a delegate, an enum and a namespace at the top level, and
    ///     before a namespace's members. Two comments are one run, and the blank line goes above it.
    /// </summary>
    [Fact]
    public void ABlockCommentBeforeATopLevelDeclaration_IsBrokenAfter() =>
        Oracle.Agrees(
            """
            /* u */ using System;
            /** u2 */ using System.Text;
            /* ga */ [assembly: System.CLSCompliant(true)]

            public class C {
            }

            /* top */ public class D { }

            /** top */ public class E { }

            /* attr */ [Obsolete] public class F { }

            /* del */ public delegate void Del();

            /* en */ public enum En { A, B }

            /* ns */ namespace N {
                /* inner */ public class H { }
                /** inner2 */ public struct I { }
                public class J { } /* after */ public class K { }
            }

            /* x */ /* y */ public class L { }

            /* multi
               line */ public class M { }
            """,
            """
            /* u */

            using System;
            /** u2 */
            using System.Text;

            /* ga */
            [assembly: System.CLSCompliant(true)]

            public class C { }

            /* top */
            public class D { }

            /** top */
            public class E { }

            /* attr */
            [Obsolete]
            public class F { }

            /* del */
            public delegate void Del();

            /* en */
            public enum En {
                A,
                B
            }

            /* ns */
            namespace N {
                /* inner */
                public class H { }

                /** inner2 */
                public struct I { }

                public class J { } /* after */

                public class K { }
            }

            /* x */ /* y */
            public class L { }

            /* multi
               line */
            public class M { }
            """
        );

    /// <summary>
    ///     ⚠ Not a comment rule: the file's own level had no plan at all, so <c>using A; using B;</c> and
    ///     <c>public class A1 { } public class A2 { }</c> stayed on one line with no comment anywhere. A
    ///     namespace that holds only a <c>using</c> is expanded too.
    /// </summary>
    [Fact]
    public void TopLevelDeclarationsOnOneLine_AreOnePerLine_WithOrWithoutAComment() =>
        Oracle.Agrees(
            """
            extern alias Ex; /* e2 */ extern alias Ey;
            using System; using System.IO;
            using System.Linq; /* u3 */ using System.Text;
            [assembly: System.CLSCompliant(true)] /* ga2 */ [assembly: System.Reflection.AssemblyTitle("x")]
            public class A1 { } public class A2 { }
            public class A3 { } /* a4 */ public class A4 { }
            namespace N { using System; public class B1 { } public class B2 { } }
            namespace N2 { using System; }
            /* n3 */ namespace N3 { }
            """,
            """
            extern alias Ex; /* e2 */
            extern alias Ey;
            using System;
            using System.IO;
            using System.Linq; /* u3 */
            using System.Text;

            [assembly: System.CLSCompliant(true)] /* ga2 */
            [assembly: System.Reflection.AssemblyTitle("x")]

            public class A1 { }

            public class A2 { }

            public class A3 { } /* a4 */

            public class A4 { }

            namespace N {
                using System;

                public class B1 { }

                public class B2 { }
            }

            namespace N2 {
                using System;
            }

            /* n3 */
            namespace N3 { }
            """
        );

    /// <summary>
    ///     A file-scoped namespace's directives and members, and the comment on the file's first line,
    ///     which no token precedes.
    /// </summary>
    [Fact]
    public void AFileScopedNamespacesLines_AreOnePerLine() =>
        Oracle.Agrees(
            """
            /* fs */ namespace FS; using System; /* u */ using System.IO;

            /* a */ public class A { }

            /** b */ public interface B { }
            /* r */ public record R(int X); public class X { } /* y */ public class Y { }
            """,
            """
            /* fs */

            namespace FS;

            using System; /* u */
            using System.IO;

            /* a */
            public class A { }

            /** b */
            public interface B { }

            /* r */
            public record R(int X);

            public class X { } /* y */

            public class Y { }
            """
        );

    /// <summary>Top-level statements are the file's members, and each takes a line.</summary>
    [Fact]
    public void TopLevelStatements_AreOnePerLine() =>
        Oracle.Agrees(
            """
            /* s1 */ System.Console.WriteLine();
            /* s2 */ var x = 1; var y = 2; /* s */ System.Console.WriteLine();
            /* i */ if (true) { }

            /* t */ public class T { }
            """,
            """
            /* s1 */

            System.Console.WriteLine();
            /* s2 */
            var x = 1;
            var y = 2; /* s */
            System.Console.WriteLine();
            /* i */
            if (true) { }

            /* t */
            public class T { }
            """
        );
}
