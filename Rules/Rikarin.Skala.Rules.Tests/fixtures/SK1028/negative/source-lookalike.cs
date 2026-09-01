namespace System.Text { class Encoding { public static Encoding UTF8 => new Encoding(); public string GetString(byte[] bytes) => "array"; public string GetString(System.ReadOnlySpan<byte> bytes) => "span"; } }
class C { string M(System.ReadOnlySpan<byte> bytes) => System.Text.Encoding.UTF8.GetString(bytes.ToArray()); }
