// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class AttributesOnOwnLine {
    [First]
    [Second]
    void JoinedOnAMethod() { }

    [First]
    int JoinedOnAField;

    [First] int JoinedOnAProperty { get; set; }

    [First] event System.Action JoinedOnAnEvent;

    [First]
    void AlreadySeparated() { }
}

[First]
class JoinedOnAType { }

[First]
record JoinedOnARecord(int X);
