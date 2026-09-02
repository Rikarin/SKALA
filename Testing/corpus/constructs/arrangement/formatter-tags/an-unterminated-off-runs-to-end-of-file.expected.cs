// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
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
