using System;

sealed class Token : IEquatable<Token> {
    public int Id { get; init; }

    public override bool Equals(object? other) => other is Token token && token.Id == Id;

    public override int GetHashCode() => Id;

    bool IEquatable<Token>.Equals(Token? other) => other is not null && other.Id == Id;
}
