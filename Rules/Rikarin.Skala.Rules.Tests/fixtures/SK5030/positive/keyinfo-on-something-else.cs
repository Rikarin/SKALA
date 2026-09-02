// ⚠ The fixture that makes the guard's type test load-bearing. The guard is "this block
// mentions `SignedXml.KeyInfo`", not "this block mentions the word KeyInfo": an unrelated
// `KeyInfo` property on somebody's own record must not buy silence for the signature check.
// Without the declaring-type test on the guard, the rule is silent here — a miss that no
// other fixture can see, because every other positive avoids the identifier entirely.
using System.Security.Cryptography.Xml;
using System.Xml;

public sealed record AuditEntry(string KeyInfo);

public static class Ledger {
    public static bool Accept(XmlDocument document, XmlElement signature, AuditEntry entry) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);

        System.Console.WriteLine(entry.KeyInfo);

        return signed.CheckSignature();
    }
}
