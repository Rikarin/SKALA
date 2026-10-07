interface ISized {
    int Length => 0;
}

interface IMeasured : ISized {
    abstract int ISized.Length { get; }
}
