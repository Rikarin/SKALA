using System;

public interface IExporter {
    void Export(string path);
}

public sealed class CsvExporter : IExporter {
    // A message that explains the shape of the work and still names no owner.
    public void Export(string path) => throw new NotImplementedException("the column order is undecided");
}
