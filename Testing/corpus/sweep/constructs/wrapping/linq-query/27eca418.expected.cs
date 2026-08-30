// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class Queries {
    void Fits(int[] numbers) {
        var query = from n in numbers where n > 0 select n;
    }

    void TooLong(int[] numbers) {
        var doubledPositives = from number in numbers
            where number > 0 && number < 100
            orderby number descending
            select number * 2;
    }

    void JoinedAndLetAndNestedFrom(int[] numbers, string[] words) {
        var query = from number in numbers
            join word in words on number.ToString() equals word into matches
            from match in matches
            let doubled = number * 2
            where doubled > 10
            orderby match
            select match;
    }

    void GroupIntoContinuation(int[] numbers) {
        var query = from number in numbers
            where number > 0
            group number by number % 3
            into bucket
            orderby bucket.Key descending
            select bucket.Key;
    }

    void BrokenByTheAuthorAtOneClause(int[] numbers) {
        var query = from n in numbers
            where n > 0
            orderby n
            select n;
    }
}
