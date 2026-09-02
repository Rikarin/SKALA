// ⚠ The compiler already makes these ONE type -- the same Type instance at run time -- so there
// is nothing here to reuse and nothing for an edit to do.
class IdenticalOrder {
    public string Build(int id, string name) {
        var head = new { Id = id, Name = name };
        var tail = new { Id = id, Name = name };
        return head.ToString() + tail.ToString();
    }
}
