// analyzer-option: dotnet_code_quality.SK7083.threshold = 1
// An interpolated string is a different expression kind and its text runs are not literals: there
// is no single token to name, and the thing that repeats is a sentence built around a value rather
// than a decision written down twice. A `u8` literal is a different kind again.
namespace Fixtures;

class Messages {
    public static string One(string name) => $"the tenant {name} is unknown";

    public static string Two(string name) => $"the tenant {name} is unknown";

    public static string Three(string name) => $"the tenant {name} is unknown";

    public static string Four(string name) => $"the tenant {name} is unknown";

    public static System.ReadOnlySpan<byte> Marker() => "the tenant marker"u8;

    public static System.ReadOnlySpan<byte> Marker2() => "the tenant marker"u8;

    public static System.ReadOnlySpan<byte> Marker3() => "the tenant marker"u8;

    public static System.ReadOnlySpan<byte> Marker4() => "the tenant marker"u8;
}
