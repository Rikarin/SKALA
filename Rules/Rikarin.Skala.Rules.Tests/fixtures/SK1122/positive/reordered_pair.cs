class ReorderedPair {
    public string Build(int id, string name) {
        var head = new { Id = id, Name = name };
        var tail = new { Name = name, Id = id };
        return head.ToString() + tail.ToString();
    }
}
