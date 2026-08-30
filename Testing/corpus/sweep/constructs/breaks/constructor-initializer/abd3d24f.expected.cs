// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
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
