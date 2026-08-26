// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-26
class AttributesOnOwnLine {
    [First]
    [Second]
    void JoinedOnAMethod() { }

    [First]
    int JoinedOnAField;

    [First]
    int JoinedOnAProperty { get; set; }

    [First]
    event System.Action JoinedOnAnEvent;

    [First]
    void AlreadySeparated() { }
}

[First]
class JoinedOnAType { }

[First]
record JoinedOnARecord(int X);
