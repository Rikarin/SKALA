class DifferentType {
    public string Build(int id, long wide, string name) {
        var head = new { Id = id, Name = name };
        var tail = new { Name = name, Id = wide };
        return head.ToString() + tail.ToString();
    }
}
