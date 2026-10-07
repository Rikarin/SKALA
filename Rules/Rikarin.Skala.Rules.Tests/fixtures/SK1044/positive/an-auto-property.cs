// An auto-property's getter is the compiler's and reads only its backing field, so one read and two
// are the same; the fix's result is pinned by Probe (#423).
public sealed class Profile {
    public string? Name { get; set; }
}

public static class Probe {
    public static int Run() {
        var profile = new Profile { Name = "" };
        return profile.Name == null || profile.Name.Length == 0 ? 1 : 0;
    }
}
