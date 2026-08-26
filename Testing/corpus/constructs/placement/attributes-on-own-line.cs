class AttributesOnOwnLine {
    [First] [Second] void JoinedOnAMethod() { }

    [First] int JoinedOnAField;

    [First] int JoinedOnAProperty { get; set; }

    [First] event System.Action JoinedOnAnEvent;

    [First]
    void AlreadySeparated() { }
}

[First] class JoinedOnAType { }

[First] record JoinedOnARecord(int X);
