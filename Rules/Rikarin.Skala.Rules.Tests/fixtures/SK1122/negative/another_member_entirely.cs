// "Nearby" is the same member. Anonymous types unify across the whole assembly, so a pair whose
// halves cannot be read side by side is not a finding a reader can act on.
class SeparateMembers {
    public string Head(int id, string name) {
        var head = new { Id = id, Name = name };
        return head.ToString();
    }

    public string Tail(int id, string name) {
        var tail = new { Name = name, Id = id };
        return tail.ToString();
    }
}
