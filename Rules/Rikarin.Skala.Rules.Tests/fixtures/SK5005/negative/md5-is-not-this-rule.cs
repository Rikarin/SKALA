using System.Security.Cryptography;

// ⚠ Also a documented cut. A content address is not a security control, and separating this from a
// password digest needs to know what the hash is compared against — a question about the value's
// use that this rule does not ask.
public static class ContentAddress {
    public static byte[] Of(byte[] blob) => MD5.HashData(blob);
}
