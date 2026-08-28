// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
