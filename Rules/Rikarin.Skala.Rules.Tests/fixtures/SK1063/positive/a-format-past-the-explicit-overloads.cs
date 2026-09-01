// ⚠ Four arguments binds `Format(string, params object[])` rather than one of the explicitly typed
// overloads, and the rule silently declined every one of those until a corpus sweep found it. No
// fixture had four arguments, which is why none of them caught it.
public sealed class Record {
    public string Line(string w, string x, string y, string z) =>
        string.Format("{{ w = {0}, x = {1}, y = {2}, z = {3} }}", w, x, y, z);
}
