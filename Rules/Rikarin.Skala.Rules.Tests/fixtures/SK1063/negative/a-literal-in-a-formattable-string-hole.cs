// ⚠ #425: a `FormattableString` keeps its text and its arguments apart, and the arguments are what a
// query builder sends as parameters. Moving the literal out of the hole moves it into `Format`:
// measured for #412's audit as `select {0} from t args=1` before the fix and
// `select name from t args=0` after it.
using System;

public static class Probe {
    public static string Run() {
        FormattableString query = $"select {"name"} from t";
        return query.Format + " args=" + query.ArgumentCount;
    }
}
