// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
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

class Primary(
    int a) : Base(a);
