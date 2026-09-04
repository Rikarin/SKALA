// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class Base {
    protected Base(int a) { }
}

class SameLine : Base {
    public SameLine(int a)
        : base(a) { }
}

class OwnLine : Base {
    public OwnLine(int a)
        : base(a) { }
}

class Primary(int a) : Base(a);
