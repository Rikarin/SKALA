// analyzer-option: dotnet_code_quality.SK7081.threshold = 5
// Exactly five. The family reports `> threshold`, so the threshold itself is silent — this is the
// fixture that proves the boundary is not off by one.
namespace Fixtures;

class One { }

class Two { }

class Three { }

class Four { }

class Five { }

class AtTheThreshold {
    One one = new();

    Two two = new();

    Three three = new();

    Four four = new();

    Five five = new();
}
