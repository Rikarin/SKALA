using System.Text.Json.Serialization;

record Point(int X, int Y) {
    [JsonPropertyName("x")]
    public int X { get; init; } = X;
}
