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
