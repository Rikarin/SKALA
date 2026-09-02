// A type of the author's own with the same four method names. Only bound `System.String` members
// are inspected, so nothing here is reported and no name has to be reserved.
class Rope {
    public int IndexOf(string value) => 0;
    public int LastIndexOf(string value) => 0;
    public bool StartsWith(string value) => false;
    public bool EndsWith(string value) => false;
}

class C {
    int First(Rope rope) => rope.IndexOf("needle");
    int Last(Rope rope) => rope.LastIndexOf("-");
    bool Prefix(Rope rope) => rope.StartsWith("sk.");
    bool Suffix(Rope rope) => rope.EndsWith(".cs");
}
