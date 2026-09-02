// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using System;

// A generic attribute — `[Cache<int>]`, C# 11 — occurred nowhere. It is not a syntax kind of its own,
// which is why the kind census could not see the gap: it is an Attribute whose Name is a GenericName,
// so it puts an Angles node inside a Brackets one and `resharper_csharp_space_before_type_argument_angle`
// meets `resharper_csharp_space_after_attributes` and the attribute-arrangement keys at the same point.
class CacheAttribute<T> : Attribute {
    public CacheAttribute() { }

    public CacheAttribute(string key) => Key = key;

    public string? Key { get; init; }
}

class PairAttribute<TFirst, TSecond> : Attribute { }

[Cache<int>]
class Bare { }

[Cache<int>("alpha")]
class WithArgument { }

[Cache<int>, Pair<string, int>]
class TwoInOneList { }

[Cache<int>]
[Pair<string, int>]
class TwoLists { }

[Pair<System.Collections.Generic.IReadOnlyDictionary<string, int>, System.Collections.Generic.IReadOnlyList<string>>]
class OverflowingTypeArguments { }

class Targets {
    [Cache<int>(Key = "the key is long enough that the attribute's named argument cannot stay on this line")]
    public int Property { get; set; }

    [return: Cache<string>]
    public string Returned() => string.Empty;

    public void Parameters([Cache<int>] int alpha, [Pair<string, int>] string bravo) { }

    public void Local() {
        [Cache<int>]
        int Nested(int subject) => subject;
    }
}
