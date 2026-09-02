# Rules

<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->

`SK` + four digits, allocated once and never re-purposed (ADR-012). 300 ids are allocated.

## Async

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK3001](SK3001.md) | `async void` outside an event handler | none | review | no |
| [SK3002](SK3002.md) | Blocking on an async call | warning | review | no |
| [SK3003](SK3003.md) | Configure awaited tasks in library code | warning | — | no |
| [SK3004](SK3004.md) | A `CancellationToken` is accepted and not passed on | warning | review | no |
| [SK3005](SK3005.md) | A task is discarded in synchronous code | warning | — | no |
| [SK3007](SK3007.md) | A `Task` that uses a `using` resource is returned instead of awaited | warning | review | no |
| [SK3009](SK3009.md) | Review explicitly unsynchronized Lazy in static state | warning | — | no |
| [SK3020](SK3020.md) | The non-`async` `Task` method returns null | warning | review | no |
| [SK3021](SK3021.md) | A `SpinLock` is stored in a `readonly` field | warning | review | no |
| [SK3030](SK3030.md) | The async iterator is enumerated without `await foreach` | warning | review | no |
| [SK3031](SK3031.md) | The method is `async` only to return an awaited task | suggestion | review | no |
| [SK3040](SK3040.md) | The `lock` is taken over a synchronization primitive | warning | — | no |
| [SK3041](SK3041.md) | The compound operation on a `volatile` field is not atomic | warning | — | no |
| [SK3042](SK3042.md) | The double-checked locking is not correct | warning | — | no |
| [SK3043](SK3043.md) | Locks are taken in inconsistent orders | warning | — | no |
| [SK3044](SK3044.md) | The field is guarded on some paths and not others | warning | — | no |
| [SK3050](SK3050.md) | An exception is thrown out of an `async void` body | warning | — | yes |
| [SK3051](SK3051.md) | The `async` method has no `CancellationToken` to forward | suggestion | review | no |
| [SK3052](SK3052.md) | The `async` lambda is converted to a `void`-returning delegate | warning | — | no |
| [SK3060](SK3060.md) | The lock is entered and not released on every path | warning | — | no |
| [SK3061](SK3061.md) | The `lock` target is not the object other threads take | warning | — | no |
| [SK3062](SK3062.md) | `this` escapes before the constructor finishes | warning | — | no |

## Cleanup

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK0230](SK0230.md) | The `with` expression or object initializer is empty | warning | review | yes |
| [SK0231](SK0231.md) | The string call produces the string it was given | warning | safe | no |
| [SK0232](SK0232.md) | The argument or signature element is redundant | warning | safe | no |
| [SK0233](SK0233.md) | The syntax is redundant | suggestion | safe | yes |
| [SK0234](SK0234.md) | The cast or type argument is redundant | warning | safe | no |
| [SK0240](SK0240.md) | The control flow does nothing | warning | safe | yes |
| [SK0241](SK0241.md) | The modifier has no effect | warning | safe | yes |
| [SK0242](SK0242.md) | The `#nullable` directive changes nothing | warning | safe | yes |
| [SK0243](SK0243.md) | The qualifier is redundant | warning | safe | no |
| [SK0244](SK0244.md) | The declaration adds nothing | warning | safe | yes |
| [SK0250](SK0250.md) | The discard designation is redundant | suggestion | safe | yes |

## Correctness

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK2001](SK2001.md) | Comparison is fixed by the integral type's range | warning | — | no |
| [SK2002](SK2002.md) | A pure method's result is discarded | warning | — | no |
| [SK2003](SK2003.md) | Review exact equality of floating-point arithmetic | warning | — | no |
| [SK2004](SK2004.md) | Typed equality has no matching object equality | warning | — | no |
| [SK2005](SK2005.md) | Do not mutate a copy of a readonly struct field | warning | — | no |
| [SK2007](SK2007.md) | The collection being enumerated is modified inside the loop | warning | review | no |
| [SK2008](SK2008.md) | A stored delegate captures a changing loop variable | warning | — | no |
| [SK2009](SK2009.md) | An enum switch omits declared members | warning | — | no |
| [SK2010](SK2010.md) | A string comparison has an implicit culture policy | warning | — | no |
| [SK2011](SK2011.md) | A struct uses inherited ValueType.Equals | warning | — | no |
| [SK2012](SK2012.md) | Review a self-operation on an automatic property | warning | — | no |
| [SK2013](SK2013.md) | An exception is constructed and then discarded | warning | safe | no |
| [SK2014](SK2014.md) | An empty catch silently discards an exception | warning | — | yes |
| [SK2015](SK2015.md) | `throw ex;` resets the stack trace | warning | safe | yes |
| [SK2016](SK2016.md) | A logger message is interpolated before it is logged | suggestion | — | no |
| [SK2017](SK2017.md) | The exception names a parameter that does not exist | warning | safe | no |
| [SK2030](SK2030.md) | Detect NaN with IsNaN rather than equality | warning | safe | no |
| [SK2031](SK2031.md) | Do not discard a setter's value parameter | warning | — | yes |
| [SK2032](SK2032.md) | Remove GC.SuppressFinalize from a type with no finalizer | suggestion | safe | no |
| [SK2033](SK2033.md) | Do not stackalloc inside a loop | warning | — | yes |
| [SK2034](SK2034.md) | Do not name a declaration after a reserved keyword | suggestion | — | yes |
| [SK2040](SK2040.md) | The comparison is by reference where value equality was meant | warning | review | no |
| [SK2041](SK2041.md) | `base.Equals` or `base.GetHashCode` resolves to `object`'s | warning | — | no |
| [SK2042](SK2042.md) | The hash code reads state that equality ignores | warning | — | no |
| [SK2043](SK2043.md) | The hash code depends on state that can change | warning | — | no |
| [SK2044](SK2044.md) | The equality members are inconsistent with each other | warning | — | no |
| [SK2050](SK2050.md) | Integer division feeds a fractional result | warning | review | no |
| [SK2051](SK2051.md) | The arithmetic has a result its constant operand already fixes | warning | safe | no |
| [SK2052](SK2052.md) | The shift count is masked to a different count | warning | — | no |
| [SK2053](SK2053.md) | The size comparison is decided by a count never being negative | warning | — | no |
| [SK2054](SK2054.md) | A signed modulus result is compared for equality with a non-zero value | warning | — | no |
| [SK2060](SK2060.md) | The condition is an assignment, not a comparison | warning | — | yes |
| [SK2061](SK2061.md) | Both operands of the operator are the same expression | warning | — | no |
| [SK2062](SK2062.md) | A later condition in the chain repeats an earlier one | warning | — | yes |
| [SK2063](SK2063.md) | The operator sequence reads as a different operator | warning | — | yes |
| [SK2064](SK2064.md) | Use && or \|\| rather than & or \| on booleans | warning | safe | no |
| [SK2070](SK2070.md) | The Serilog message template has a different number of holes than the call supplies values | warning | — | no |
| [SK2071](SK2071.md) | The structured log template names the same property twice | warning | — | no |
| [SK2072](SK2072.md) | The literal contains an unescaped invisible character | warning | safe | yes |
| [SK2073](SK2073.md) | The caught exception is not passed to the logger's exception parameter | warning | safe | no |
| [SK2080](SK2080.md) | The set or dictionary initializer repeats a key | warning | — | no |
| [SK2081](SK2081.md) | The collection is passed to its own method as the other collection | warning | — | no |
| [SK2082](SK2082.md) | The collection element is written twice with nothing reading it in between | warning | — | no |
| [SK2083](SK2083.md) | The collection iterated here is provably empty | warning | — | no |
| [SK2090](SK2090.md) | The finalizer can throw | warning | — | no |
| [SK2091](SK2091.md) | An exception is thrown from a `finally` block | warning | — | yes |
| [SK2092](SK2092.md) | `NullReferenceException` is caught | warning | — | yes |
| [SK2093](SK2093.md) | The handler discards the exception it caught | warning | review | no |
| [SK2100](SK2100.md) | `[ThreadStatic]` is applied where it cannot take effect | warning | review | no |
| [SK2101](SK2101.md) | `[Pure]` is applied to a method that returns nothing | warning | safe | no |
| [SK2102](SK2102.md) | The `[DebuggerDisplay]` string names a member that does not exist | warning | — | no |
| [SK2103](SK2103.md) | The attribute is applied twice with the same arguments | warning | review | no |
| [SK2110](SK2110.md) | `ToString()` returns null | warning | review | no |
| [SK2111](SK2111.md) | The null-forgiving operator suppresses nothing | warning | safe | no |
| [SK2112](SK2112.md) | The local is declared nullable and is never null | suggestion | safe | no |
| [SK2113](SK2113.md) | The service resolution is null-forgiven instead of required | suggestion | safe | no |
| [SK2120](SK2120.md) | A bitwise operator combines members of a consecutively numbered enum | warning | — | no |
| [SK2121](SK2121.md) | The `as` operator tests a conversion that always succeeds | suggestion | safe | no |
| [SK2130](SK2130.md) | A static field initializer reads a field declared below it | warning | — | no |
| [SK2131](SK2131.md) | The get-only property is never assigned | warning | — | no |
| [SK2132](SK2132.md) | The accessor uses another property's backing field | warning | review | no |
| [SK2133](SK2133.md) | The called `partial` method has no implementation | suggestion | — | no |
| [SK2134](SK2134.md) | Instance code writes to its own type's static field | warning | — | no |
| [SK2140](SK2140.md) | The override declares a parameter default or `params` the call site will not use | warning | review | no |
| [SK2141](SK2141.md) | The argument replaces what the caller-info attribute would have supplied | warning | review | no |
| [SK2142](SK2142.md) | The value passed for the parameter is overwritten before it is read | warning | — | no |
| [SK2143](SK2143.md) | Two arguments are passed crosswise to the parameters they are named after | warning | review | no |
| [SK2150](SK2150.md) | A string search has an implicit culture policy | warning | review | no |
| [SK2151](SK2151.md) | A string equality test asks for invariant culture where ordinal was meant | warning | review | no |
| [SK2152](SK2152.md) | The path comparison assumes the file system's case rules | warning | — | no |
| [SK2153](SK2153.md) | An IQueryable is silently degraded to IEnumerable | warning | — | no |
| [SK2154](SK2154.md) | The sort has no ordering to use | warning | — | no |
| [SK2160](SK2160.md) | The clock is read from a static | none | — | no |
| [SK2161](SK2161.md) | The `DateTime` has no time zone and is converted as if it had one | warning | — | no |
| [SK2162](SK2162.md) | The date or time `TryParse` has an implicit culture | warning | — | no |
| [SK2163](SK2163.md) | Elapsed time is measured with the wall clock | warning | review | no |
| [SK2164](SK2164.md) | The assertion's expression has side effects | warning | — | no |
| [SK2170](SK2170.md) | The indentation says the statement is in the body and it is not | warning | — | yes |
| [SK2171](SK2171.md) | The `\x` escape's length is decided by what follows it | warning | safe | yes |
| [SK2172](SK2172.md) | The `!` before `is` reads as a negated `is` | warning | — | no |
| [SK2173](SK2173.md) | `is not { }` is a null check written backwards | warning | safe | yes |
| [SK2174](SK2174.md) | The shift or bitwise operand relies on precedence to group | suggestion | safe | yes |
| [SK2180](SK2180.md) | The `foreach` casts every element down to a narrower type | warning | — | no |
| [SK2181](SK2181.md) | `GetType()` is called on something that is already a `Type` | warning | review | no |
| [SK2182](SK2182.md) | A type is identified by comparing its name to a string | warning | review | no |
| [SK2183](SK2183.md) | A static member is reached through a type that inherits it | suggestion | safe | no |
| [SK2184](SK2184.md) | The call skips a better overload the derived interface hides | warning | — | no |
| [SK2190](SK2190.md) | The struct key has no equality of its own | warning | — | no |
| [SK2191](SK2191.md) | The struct is copied before the mutation lands | warning | — | no |
| [SK2192](SK2192.md) | `==` on spans compares memory locations | warning | review | no |
| [SK2193](SK2193.md) | `ImmutableArray<T>` is built with a collection initializer | error | review | no |
| [SK2194](SK2194.md) | The captured primary constructor parameter is assigned | warning | — | no |
| [SK2200](SK2200.md) | The field initializer is overwritten by every constructor | warning | safe | no |
| [SK2201](SK2201.md) | The unsubscription passes an anonymous function | warning | — | no |
| [SK2202](SK2202.md) | The modification sits inside a conditional invocation | warning | — | yes |

## Design

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK6003](SK6003.md) | An abstract type has a public constructor | suggestion | safe | yes |
| [SK6008](SK6008.md) | An extension method extends `object` | suggestion | — | no |
| [SK6020](SK6020.md) | An `Enum` constraint has no `struct` beside it | warning | review | no |
| [SK6021](SK6021.md) | A type is named like an exception and is not one | warning | — | no |
| [SK6022](SK6022.md) | A type name repeats the keyword the declaration already carries | suggestion | — | yes |
| [SK6023](SK6023.md) | A type has no members, no base and no attributes | suggestion | — | yes |
| [SK6030](SK6030.md) | A type is declared in the global namespace | warning | — | yes |
| [SK6031](SK6031.md) | `readonly` does not protect a mutable field's contents | warning | — | no |
| [SK6032](SK6032.md) | An abstract type declares nothing to implement | suggestion | — | yes |
| [SK6033](SK6033.md) | A type has only private constructors and is not static | warning | — | no |
| [SK6034](SK6034.md) | A public constant is baked into every caller | suggestion | review | no |
| [SK6040](SK6040.md) | An `out` variable is declared and never read | warning | safe | no |
| [SK6041](SK6041.md) | A `foreach` variable is declared wider than the element it receives | suggestion | review | no |
| [SK6050](SK6050.md) | The method ignores its inputs and returns a constant | suggestion | — | no |
| [SK6051](SK6051.md) | The base type tests `this` against a derived type | suggestion | — | no |
| [SK6052](SK6052.md) | Null is returned where an empty sequence was expected | suggestion | review | no |
| [SK6053](SK6053.md) | The method name does not reflect its synchronicity | none | — | no |
| [SK6060](SK6060.md) | The interface's type parameter is invariant and need not be | suggestion | review | no |
| [SK6061](SK6061.md) | A caller-info parameter is followed by an ordinary parameter | warning | review | no |
| [SK6062](SK6062.md) | The local collection is filled and never read | suggestion | — | no |

## Formatting

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK0001](SK0001.md) | The file is not formatted | suggestion | safe | yes |
| [SK0002](SK0002.md) | The line is over the width and nothing in it can break | hint | — | yes |
| [SK0003](SK0003.md) | The documentation comment is not well-formed XML | hint | — | yes |
| [SK0201](SK0201.md) | The body style is not arranged | suggestion | — | yes |
| [SK0202](SK0202.md) | The local variable type is not arranged | suggestion | — | no |
| [SK0203](SK0203.md) | The object creation is not arranged | suggestion | — | no |
| [SK0204](SK0204.md) | The default value expression is not arranged | suggestion | — | no |
| [SK0205](SK0205.md) | The null check is not arranged | suggestion | — | no |
| [SK0206](SK0206.md) | The empty string expression is not arranged | suggestion | — | no |
| [SK0207](SK0207.md) | The instance-member qualifier is not arranged | suggestion | — | no |
| [SK0208](SK0208.md) | The control-statement braces are not arranged | suggestion | — | yes |
| [SK0209](SK0209.md) | The expression parentheses are not arranged | suggestion | — | yes |
| [SK0210](SK0210.md) | The using directives are not arranged | suggestion | — | yes |
| [SK0211](SK0211.md) | The predefined type spelling is not arranged | suggestion | — | no |
| [SK0212](SK0212.md) | The accessibility modifier is not arranged | suggestion | — | yes |
| [SK0213](SK0213.md) | The namespace declaration is not arranged | suggestion | — | yes |
| [SK0214](SK0214.md) | The trailing comma is not arranged | suggestion | — | yes |
| [SK0215](SK0215.md) | The static-member qualifier is not arranged | suggestion | — | no |
| [SK0216](SK0216.md) | The argument naming style is not arranged | suggestion | — | no |
| [SK0217](SK0217.md) | The discard declaration is not arranged | suggestion | — | yes |

## Lifetime

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK3501](SK3501.md) | A disposable is created in a local and never disposed | warning | safe | no |
| [SK3502](SK3502.md) | A type owns a disposable field but is not disposable | warning | — | no |
| [SK3503](SK3503.md) | An `IAsyncDisposable` is disposed synchronously | warning | review | no |
| [SK3510](SK3510.md) | A variable already owned by `using` is disposed again | warning | safe | no |
| [SK3511](SK3511.md) | The `using` resource is built with an object initializer | warning | safe | no |
| [SK3512](SK3512.md) | A variable captured by `using` is returned | warning | — | no |
| [SK3530](SK3530.md) | The disposable field is not disposed by `Dispose` | warning | review | no |
| [SK3531](SK3531.md) | The `DisposeAsync` override never calls the base implementation | warning | — | no |
| [SK3532](SK3532.md) | The `ref struct` owns a disposable and cannot declare it | warning | — | no |
| [SK3540](SK3540.md) | A `Dispose` method the type never declares | warning | review | no |
| [SK3541](SK3541.md) | The `HttpClient` is disposed at the end of the call | warning | — | no |
| [SK3542](SK3542.md) | `DangerousGetHandle` with nothing holding the handle open | warning | — | no |

## Maintainability

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK7001](SK7001.md) | Cyclomatic complexity is over the threshold | hint | — | no |
| [SK7002](SK7002.md) | Cognitive complexity is over the threshold | suggestion | — | yes |
| [SK7003](SK7003.md) | The member is over the statement-count threshold | hint | — | yes |
| [SK7004](SK7004.md) | The type is over the member-count threshold | hint | — | yes |
| [SK7005](SK7005.md) | The member takes more parameters than the threshold | hint | — | yes |
| [SK7006](SK7006.md) | The member nests deeper than the threshold | hint | — | yes |
| [SK7010](SK7010.md) | Public API without a documentation comment | none | — | yes |
| [SK7020](SK7020.md) | Duplicated block | warning | — | no |
| [SK7030](SK7030.md) | File length exceeds the configured threshold | hint | — | yes |
| [SK7040](SK7040.md) | TODO or FIXME has no issue reference | suggestion | — | yes |
| [SK7050](SK7050.md) | A warning-disable pragma has no justification | warning | — | yes |
| [SK7051](SK7051.md) | A suppression attribute has no justification | warning | — | no |
| [SK7060](SK7060.md) | Review commented-out statements | hint | — | yes |
| [SK7070](SK7070.md) | An obsolete marker has no message | warning | — | no |
| [SK7071](SK7071.md) | A coverage exclusion has no justification | warning | — | no |
| [SK7072](SK7072.md) | A warning suppression covers nothing | warning | safe | yes |
| [SK7073](SK7073.md) | A region is empty | suggestion | safe | yes |
| [SK7074](SK7074.md) | Review a goto to a label | warning | — | yes |
| [SK7080](SK7080.md) | The inheritance chain is deeper than the threshold | hint | — | no |
| [SK7081](SK7081.md) | The type depends on more other types than the threshold | hint | — | no |
| [SK7082](SK7082.md) | The conditional expressions are nested | suggestion | — | yes |
| [SK7083](SK7083.md) | The string literal is repeated | hint | — | yes |
| [SK7090](SK7090.md) | A thrown `NotImplementedException` has no issue reference | warning | — | no |
| [SK7091](SK7091.md) | The process is terminated outside the entry point | warning | — | no |
| [SK7092](SK7092.md) | The exception is both logged and rethrown | warning | — | no |
| [SK7093](SK7093.md) | The console is written to where a logger was meant | warning | — | no |
| [SK7100](SK7100.md) | The documentation duplicates the base member's | suggestion | safe | no |
| [SK7101](SK7101.md) | A non-public member has no documentation comment | none | — | yes |
| [SK7110](SK7110.md) | The logger is declared for a different type than the one that declares it | suggestion | safe | no |

## Modernization

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK1001](SK1001.md) | Use a collection expression | suggestion | safe | no |
| [SK1003](SK1003.md) | Use a field-backed property | suggestion | safe | no |
| [SK1005](SK1005.md) | Use a file-scoped namespace | suggestion | safe | yes |
| [SK1006](SK1006.md) | Use a `using` declaration | suggestion | safe | yes |
| [SK1010](SK1010.md) | Use `is null` / `is not null` instead of `==` / `!=` | suggestion | safe | no |
| [SK1011](SK1011.md) | Use a property pattern | suggestion | safe | no |
| [SK1012](SK1012.md) | Use a switch expression for a returning equality chain | suggestion | safe | no |
| [SK1013](SK1013.md) | Use a list pattern for guarded element checks | suggestion | safe | no |
| [SK1014](SK1014.md) | Use relational and logical patterns | suggestion | safe | no |
| [SK1015](SK1015.md) | Use `is T t` instead of `is T` and a cast | suggestion | safe | no |
| [SK1020](SK1020.md) | Use `ArgumentNullException.ThrowIfNull` | suggestion | safe | no |
| [SK1022](SK1022.md) | Precompute a constant character search set | hint | safe | no |
| [SK1023](SK1023.md) | Use a dedicated System.Threading.Lock | suggestion | safe | no |
| [SK1025](SK1025.md) | Freeze a private lookup-only dictionary | hint | safe | no |
| [SK1026](SK1026.md) | Use a UTF-8 literal for constant ASCII bytes | suggestion | safe | no |
| [SK1028](SK1028.md) | Decode a byte span without an array copy | suggestion | safe | no |
| [SK1030](SK1030.md) | Use `??=` | suggestion | safe | yes |
| [SK1031](SK1031.md) | Use a null-conditional assignment | suggestion | safe | no |
| [SK1033](SK1033.md) | Use `TryGetValue` or `TryAdd` instead of `ContainsKey` and a second lookup | suggestion | safe | no |
| [SK1034](SK1034.md) | Use the `Count` property, not `Count()` or `Any()` | suggestion | safe | no |
| [SK1035](SK1035.md) | Use `Enum.GetValues<T>()` | suggestion | safe | no |
| [SK1040](SK1040.md) | Use `T?` instead of `Nullable<T>` | suggestion | safe | no |
| [SK1041](SK1041.md) | Use a compound assignment | suggestion | safe | yes |
| [SK1042](SK1042.md) | The nested `if` statements can be combined | suggestion | safe | yes |
| [SK1043](SK1043.md) | The `for` loop is a `while` | suggestion | safe | yes |
| [SK1044](SK1044.md) | Use `string.IsNullOrEmpty` | suggestion | safe | no |
| [SK1050](SK1050.md) | Use pattern matching instead of a test-and-cast | suggestion | safe | no |
| [SK1051](SK1051.md) | Simplify the pattern | suggestion | safe | no |
| [SK1052](SK1052.md) | Merge the `?:` into a conditional access | suggestion | safe | no |
| [SK1053](SK1053.md) | Use a discard | suggestion | safe | no |
| [SK1054](SK1054.md) | Inline the `out` variable declaration | suggestion | safe | no |
| [SK1060](SK1060.md) | Use an index-from-end expression | suggestion | safe | no |
| [SK1061](SK1061.md) | Use `nameof` | suggestion | safe | no |
| [SK1062](SK1062.md) | Use the string literal form that needs no escapes | hint | safe | yes |
| [SK1063](SK1063.md) | Use the interpolation form that says what it means | suggestion | safe | no |
| [SK1064](SK1064.md) | Use `>>>` | suggestion | safe | no |
| [SK1070](SK1070.md) | Deconstruct the tuple instead of reading it element by element | suggestion | safe | no |
| [SK1071](SK1071.md) | Copy the record with a `with` expression | suggestion | safe | no |
| [SK1072](SK1072.md) | The spread of a freshly created array is its elements | suggestion | safe | no |
| [SK1073](SK1073.md) | Use the framework's cached instance | suggestion | safe | no |
| [SK1080](SK1080.md) | Use the LINQ operator that says what the chain means | suggestion | safe | no |
| [SK1081](SK1081.md) | Remove the LINQ or collection call that does nothing | warning | safe | no |
| [SK1082](SK1082.md) | Use the indexer | suggestion | safe | no |
| [SK1083](SK1083.md) | Use `foreach` over an indexed `for` | suggestion | review | no |
| [SK1084](SK1084.md) | Use a LINQ operator for part of the loop | hint | review | no |
| [SK1090](SK1090.md) | Use a computed property instead of an auto-property holding a constant | hint | review | no |
| [SK1091](SK1091.md) | The private auto-property is a field | suggestion | safe | no |
| [SK1092](SK1092.md) | Use a tuple literal instead of constructing a `Tuple` | suggestion | review | no |
| [SK1093](SK1093.md) | Express the cast in the declaration instead | hint | safe | no |
| [SK1094](SK1094.md) | Use nullable reference type syntax instead of an annotation attribute | warning | review | no |

## Performance

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK4001](SK4001.md) | Review LINQ in a configured hot path | none | — | no |
| [SK4002](SK4002.md) | Review iteration-local delegate captures | hint | — | no |
| [SK4003](SK4003.md) | Review a temporary params array with a span overload | hint | — | no |
| [SK4004](SK4004.md) | Review boxing despite an existing generic constraint | hint | — | no |
| [SK4006](SK4006.md) | Review a materialization used only by foreach | hint | — | no |
| [SK4007](SK4007.md) | Review large struct arguments in loops | hint | — | no |
| [SK4010](SK4010.md) | A `Where` the next operator could have taken as its predicate | suggestion | safe | no |
| [SK4020](SK4020.md) | The lambda captures nothing and is not `static` | suggestion | safe | no |
| [SK4021](SK4021.md) | The private method does not use instance state | hint | safe | no |
| [SK4022](SK4022.md) | The struct is never mutated and is not `readonly` | suggestion | safe | no |
| [SK4023](SK4023.md) | The capacity argument matches the default | warning | safe | no |
| [SK4024](SK4024.md) | `GC.Collect` is called from application code | warning | — | no |
| [SK4030](SK4030.md) | The collection's own method answers this faster than the LINQ extension | suggestion | safe | no |
| [SK4031](SK4031.md) | The loop looks up a key it is already holding | warning | safe | no |
| [SK4032](SK4032.md) | `Substring` is called to feed a search that takes a start index | suggestion | safe | no |
| [SK4033](SK4033.md) | The `ConcurrentDictionary` member taken is the expensive one | warning | safe | no |
| [SK4034](SK4034.md) | The collection is sorted before it is filtered | suggestion | safe | no |
| [SK4040](SK4040.md) | The property copies a collection it could have returned unchanged | suggestion | review | no |
| [SK4041](SK4041.md) | The `StringBuilder` is filled and never read | warning | — | no |

## Security

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK5001](SK5001.md) | Request data is concatenated into SQL | error | — | no |
| [SK5002](SK5002.md) | Request data reaches a process start | error | — | no |
| [SK5005](SK5005.md) | A broken cipher, or a mode that leaks structure | error | — | no |
| [SK5007](SK5007.md) | A certificate callback that accepts everything | error | — | no |
| [SK5009](SK5009.md) | An XML reader that parses a DTD and fetches what it names | error | — | no |
| [SK5010](SK5010.md) | A pattern that can backtrack, with nothing bounding how long it may take | warning | — | no |
| [SK5020](SK5020.md) | A cipher initialisation vector fixed at compile time | error | — | no |
| [SK5021](SK5021.md) | An RSA or DSA key generated below 2048 bits | error | review | no |

## Tests

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK8005](SK8005.md) | `Thread.Sleep` in a test | suggestion | — | no |
| [SK8006](SK8006.md) | A skipped test has no reason | warning | — | no |
| [SK8007](SK8007.md) | Use controlled assertion input | suggestion | — | no |
| [SK8020](SK8020.md) | A class with `[TestMethod]` members carries no `[TestClass]` | warning | review | no |
| [SK8021](SK8021.md) | The test class declares no test | warning | — | no |
| [SK8022](SK8022.md) | The assertion's expected and actual arguments are swapped | warning | safe | no |

## Tool

| Id | Rule | Severity | Fix | Loose mode |
|---|---|---|---|---|
| [SK9001](SK9001.md) | Unknown configuration key | suggestion | — | no |
| [SK9010](SK9010.md) | The file does not parse | warning | — | yes |
| [SK9011](SK9011.md) | A member's braces are split across a preprocessor branch | suggestion | — | yes |
| [SK9020](SK9020.md) | The binlog is stale for this file | suggestion | — | no |
| [SK9021](SK9021.md) | The binlog does not name a file that exists | warning | — | no |
| [SK9030](SK9030.md) | An analyzer threw | warning | — | no |
| [SK9031](SK9031.md) | An analyzer package failed to load | warning | — | no |
| [SK9099](SK9099.md) | The formatter's output was not token-equivalent | error | — | yes |

