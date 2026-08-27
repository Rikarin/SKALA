// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
using System.Collections.Generic;

namespace FormatterTags;

public class AnUnterminatedOffRunsToEndOfFile {
    public List<int> Before() {
        return new List<int>();
    }

    // @formatter:off
    public  int  After( )   { return 1; }
    public List<int> Also()  { return new List<int>(); }
}
