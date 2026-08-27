// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
using System.Text;
using System;
using Alpha.Things;
using System.Globalization;
using System.Collections.Generic;
using Alias = System.Collections.Generic.List<int>;
using static System.Math;

namespace Skala.Corpus.Arrangement;

// sort_usings = true with dotnet_sort_system_directives_first = FALSE — System is not hoisted, the
// order is plain ordinal, so Alpha.Things sorts first — and removal of the ones nothing needs.
public class SortAndRemove {
    public void Used() {
        var builder = new StringBuilder();
        builder.Append(Max(1, 2).ToString(CultureInfo.InvariantCulture));
        builder.Append(Tool.Twice(3).ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(builder.ToString());
    }

    public Alias Aliased() {
        return new Alias();
    }
}
