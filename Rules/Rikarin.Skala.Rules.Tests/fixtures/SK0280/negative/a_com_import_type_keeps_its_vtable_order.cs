using System.Runtime.InteropServices;

[ComImport]
[Guid("11111111-1111-1111-1111-111111111111")]
interface IReader {
    int Read();
}

[ComImport]
[Guid("22222222-2222-2222-2222-222222222222")]
interface ISeekableReader : IReader {
    void Seek(int offset);
}

[ComImport]
[Guid("33333333-3333-3333-3333-333333333333")]
interface IBoth : ISeekableReader, IReader { }
