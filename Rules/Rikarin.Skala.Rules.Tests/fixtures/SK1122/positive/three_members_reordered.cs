class ThreeMembers {
    public string Build(int id, string name, bool ready) {
        var head = new { Id = id, Name = name, Ready = ready };
        var tail = new { Ready = ready, Id = id, Name = name };
        return head.ToString() + tail.ToString();
    }
}
