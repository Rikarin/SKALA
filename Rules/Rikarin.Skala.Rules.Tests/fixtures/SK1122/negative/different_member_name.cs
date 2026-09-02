// A different member name is a genuinely different shape. No reordering makes these one type.
class DifferentName {
    public string Build(int id, string name) {
        var head = new { Id = id, Name = name };
        var tail = new { Key = id, Name = name };
        return head.ToString() + tail.ToString();
    }
}
