using System.Collections.Generic;
using System.Text;

public sealed class Waster {
    public static void Run() {
        // Constructing anything else as a statement is somebody else's rule; SK2013 is only ever
        // about an exception nobody throws.
        new List<int>();
        new StringBuilder();
    }
}
