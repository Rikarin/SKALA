// A method of the caller's own that happens to be spelled `Pbkdf2`. The receiver is resolved
// through the compilation rather than matched by name.
public static class Vendor {
    public static byte[] Pbkdf2(string password, byte[] salt, int iterations) => salt;
}

public static class Credentials {
    public static byte[] Derive(string password) => Vendor.Pbkdf2(password, new byte[16], 1000);
}
