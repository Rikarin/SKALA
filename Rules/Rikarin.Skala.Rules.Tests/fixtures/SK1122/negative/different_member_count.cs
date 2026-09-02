class DifferentCount {
    public string Build(int id, string name, bool ready) {
        var head = new { Id = id, Name = name };
        var tail = new { Name = name, Id = id, Ready = ready };
        return head.ToString() + tail.ToString();
    }
}
