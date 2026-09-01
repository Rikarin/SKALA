// The anti-vacuity fixture: if the rule fired here it would fire on everything.
namespace Fixtures;

sealed class Keys {
    public const string Tenant = "tenant-id";
    public const string Path = "a/b/c";
    public const char Slash = '/';
}
