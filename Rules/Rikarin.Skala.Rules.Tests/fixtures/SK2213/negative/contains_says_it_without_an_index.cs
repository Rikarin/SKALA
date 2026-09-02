// What the corrected code usually becomes.
using System.Collections.Generic;

class C {
    bool Present(string path) => path.Contains(':');

    bool InList(List<int> values, int needle) => values.Contains(needle);
}
