// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
