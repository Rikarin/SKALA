using System.Collections.Generic;

namespace FormatterTags;

public class ARegionSurvivesArrangement {
    // @formatter:off
    static readonly int[,] Table = {
        { 1,   2,   3 },
        { 700, 800, 900 },
    };
    public  int  Old( )   { return 1; }
    public List<int> Made()  { return new List<int>(); }
    private System.Int32 Width() { return 3; }
    // @formatter:on

    public int New() { return 2; }
    public List<int> Other() { return new List<int>(); }
}
