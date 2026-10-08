// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// Issue #429. The file's own level had no break plan, so a block comment before a top-level
// declaration stayed on its line, and so did two declarations, two usings or two [assembly: ...]
// lists written on one. The oracle puts each on a line of its own, after any comment before it.
/* u */

using System;
/** u2 */
using System.Text;
using System.IO;
using System.Linq;

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

public class Z1 { }

public class Z2 { }

public class Z3 { } /* a4 */

public class Z4 { }

namespace N4 {
    using System;

    public class B1 { }

    public class B2 { }
}

namespace N5 {
    using System;
}

/* n3 */
namespace N6 { }
