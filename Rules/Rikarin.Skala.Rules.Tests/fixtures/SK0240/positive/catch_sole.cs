using System.IO;

class C {
    static void Write(string path, string payload) { }

    public static void Save(string path, string payload) {
        try {
            Write(path, payload);
        } catch (IOException) {
            throw;
        }
    }
}
