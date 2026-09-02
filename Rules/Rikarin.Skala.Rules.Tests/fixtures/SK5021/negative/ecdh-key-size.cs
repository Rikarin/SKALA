using System.Security.Cryptography;

// The same, for key agreement.
public static class Agreement {
    public static ECDiffieHellman Party() {
        var party = ECDiffieHellman.Create();
        party.KeySize = 256;
        return party;
    }
}
