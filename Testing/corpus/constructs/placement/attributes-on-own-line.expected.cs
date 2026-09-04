// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
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
