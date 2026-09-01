// Conservative: a name the author mentioned is a name the author had in mind.
class C {
    string last = "";

    public int Tracked {
        get => last.Length;
        set { last = nameof(value); }
    }
}
