using System.IO;

// ⚠ The refuted half of issue #145, kept as a fixture so the refutation is asserted rather than
// only written down. `Directory.CreateTempSubdirectory` creates at 0700 and `Path.GetTempFileName`
// creates at 0600 through `mkstemp`, so neither is a world-accessibility defect on .NET.
public static class Store {
    public static string Scratch() => Directory.CreateTempSubdirectory("skala").FullName;

    public static string Temporary() => Path.GetTempFileName();
}
