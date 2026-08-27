using System.Security.Cryptography;
using System.Text;

// ⚠ This fixture documents a cut rather than a capability. The id was allocated for the cipher
// half of doc 08's sentence only. An RFC 6455 WebSocket handshake is *defined* as a SHA-1 of the
// client key and a fixed GUID; the digest is not a security control and cannot be changed without
// ceasing to speak the protocol, so a report here would be wrong rather than merely unwelcome.
public static class WebSocketUpgrade {
    const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    public static byte[] Accept(string key) => SHA1.HashData(Encoding.ASCII.GetBytes(key + AcceptGuid));
}
