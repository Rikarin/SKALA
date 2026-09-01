using System.Collections.Generic;

public enum Channel {
    Red,
    Green,
    Blue
}

public sealed class Depths {
    public static readonly Dictionary<Channel, int> Bits = new() {
        [Channel.Red] = 8,
        [Channel.Green] = 8,
        [Channel.Red] = 16
    };
}
