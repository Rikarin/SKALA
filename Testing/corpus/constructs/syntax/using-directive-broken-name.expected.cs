// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A using directive's name broken by the author (#554): the oracle keeps the break and puts the rest
// of the name one level in, after a dot or before one, under `static` and `global` alike, as an alias
// and a file-scoped namespace's name already did. Skala wrote it at column 0: a using directive owned
// no continuation frame, so nothing paid for the break.

using System.
    Text;
using System
    .Collections;
using static System.
    Math;
using Alias =
    System.IO;
using Alias2 = System.
    IO;
global using System.
    Linq;
using System.Collections.
    Generic.
    Specialized;

namespace N.
    M;

class C {
    void M() { }
}
