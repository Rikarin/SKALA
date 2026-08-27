// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
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
