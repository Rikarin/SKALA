namespace Contoso.Design;

public sealed class Transport {
    sealed class Defaults {
        public const string Endpoint = "https://example.invalid/v1";
    }

    public string Endpoint => Defaults.Endpoint;
}
