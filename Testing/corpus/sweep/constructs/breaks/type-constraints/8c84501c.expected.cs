// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class SameLine<T, U> where T : struct where U : class {
    void M<V>(V v) where V : notnull { }
}

class OwnLines<T, U>
    where T : struct
    where U : class { }
