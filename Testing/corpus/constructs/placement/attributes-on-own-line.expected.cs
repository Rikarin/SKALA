// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
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
