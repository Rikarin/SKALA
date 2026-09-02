class CommentedCreation {
    public string Build(int id, string name) {
        var head = new { Id = id, Name = name };
        var tail = new {
            Name = name, // the display name leads, deliberately
            Id = id
        };

        return head.ToString() + tail.ToString();
    }
}
