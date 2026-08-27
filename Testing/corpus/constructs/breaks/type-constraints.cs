class SameLine<T, U> where T : struct where U : class {
    void M<V>(V v) where V : notnull { }
}

class OwnLines<T, U>
    where T : struct
    where U : class { }
