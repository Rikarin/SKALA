using System.Security.Cryptography;

// ⚠ The protocol-fixed derivation shape, and the reason HKDF is not a receiver. RFC 5869 says
// HKDF's salt is optional and may be fixed and public: it extracts from high-entropy input keying
// material rather than from a password, and both ends of a protocol must derive the same key from
// the same inputs. This is correct code and the rule must never claim it.
public static class Session {
    static readonly byte[] ProtocolSalt = { 0x53, 0x4b, 0x41, 0x4c, 0x41, 0x2d, 0x76, 0x31 };

    public static byte[] TrafficKey(byte[] sharedSecret) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, ProtocolSalt);
}
