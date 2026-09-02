class SingleMember {
    public string Build(int id) {
        var head = new { Id = id };
        var tail = new { Id = id };
        return head.ToString() + tail.ToString();
    }
}
