using System.Runtime.CompilerServices;

public sealed class Record {
    public Record(string message, [CallerFilePath] string file = "", int level = 0) {
        Message = message;
        File = file;
        Level = level;
    }

    public string Message { get; }

    public string File { get; }

    public int Level { get; }
}
