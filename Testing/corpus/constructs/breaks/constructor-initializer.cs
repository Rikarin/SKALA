class Base {
    protected Base(int a) { }
}

class SameLine : Base {
    public SameLine(int a) : base(a) { }
}

class OwnLine : Base {
    public OwnLine(int a)
        : base(a) { }
}

class Primary(int a) : Base(a);
