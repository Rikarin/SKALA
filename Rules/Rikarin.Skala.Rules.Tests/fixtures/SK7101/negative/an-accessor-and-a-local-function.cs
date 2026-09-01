/// <summary>A set of components stored together.</summary>
public sealed class Archetype {
    /// <summary>How many entities the archetype holds.</summary>
    internal int Count {
        // An accessor is not a thing anybody writes a `<summary>` for, and neither is a local
        // function. Both are excluded by the predicate this rule shares with `SK7010`.
        get {
            int Read() => 0;

            return Read();
        }
    }
}
