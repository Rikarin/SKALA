// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class SameLine<T, U> where T : struct where U : class {
    void M<V>(V v) where V : notnull { }
}

class OwnLines<T, U>
    where T : struct
    where U : class { }
