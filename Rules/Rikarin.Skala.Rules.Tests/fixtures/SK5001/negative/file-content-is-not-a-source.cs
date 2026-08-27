using System.Data;
using System.IO;

// A file the program was pointed at sits at the trust level of whoever pointed it there.
public static class Import {
    public static void Run(IDbCommand command, string path) {
        var name = File.ReadAllText(path);
        command.CommandText = "select * from imports where name = '" + name + "'";
    }
}
