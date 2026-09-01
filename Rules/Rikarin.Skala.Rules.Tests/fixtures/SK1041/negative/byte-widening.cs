public sealed class Bytes {
    byte level;

    // ⚠ `level = level + 1;` does not compile: `byte + int` is `int` and there is no implicit
    // narrowing back. `level += 1;` does compile, because C# defines the compound form as
    // `level = (byte)(level + 1)`. The long form the author had to write instead is this one, with
    // the cast made explicit — and the rule leaves casts alone, so this shape is silent.
    public void Raise() {
        level = (byte)(level + 1);
    }

    public byte Value => level;
}
