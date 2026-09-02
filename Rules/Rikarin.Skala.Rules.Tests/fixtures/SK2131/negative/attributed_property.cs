using System.Runtime.Serialization;

// An attribute is a standing signal that something outside the language writes this member —
// a serializer, a generator, a binder — and that traffic is invisible to any compilation-local
// analysis. Declined rather than guessed at.
[DataContract]
sealed class Payload {
    [DataMember]
    public int Version { get; }

    public bool IsCurrent => Version == 3;
}
