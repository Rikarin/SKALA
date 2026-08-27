# SK-DIV-0005 — where the oracle stops taking the `=` break

Each cell is the longest continuation line the oracle still writes rather than
wrapping the right-hand side. The statement is `var <name> = <rhs>;` with the
right-hand side padded to a known length and the *name* padded so that the flat
line comes to exactly `total` — which sweeps the continuation width independently
of how far over the margin the line was. `predicted` is milestone 3's
`120 - (8 + column / 4)`.

## `wrap_before_eq = false` — the export's value

| shape | depth | column | total | longest `=`-break line | predicted | delta |
|---|---:|---:|---:|---:|---:|---:|
| object-initializer | 2 | 12 | 121 | 109 | 109 | 0 |
| object-initializer | 2 | 12 | 137 | 107 | 109 | -2 |
| object-initializer | 3 | 16 | 121 | 109 | 108 | +1 |
| object-initializer | 3 | 16 | 137 | 106 | 108 | -2 |
| object-initializer | 4 | 20 | 121 | 108 | 107 | +1 |
| object-initializer | 4 | 20 | 137 | 106 | 107 | -1 |
| object-initializer | 5 | 24 | 121 | 107 | 106 | +1 |
| object-initializer | 5 | 24 | 137 | 105 | 106 | -1 |
| object-initializer | 6 | 28 | 121 | 107 | 105 | +2 |
| object-initializer | 6 | 28 | 137 | 104 | 105 | -1 |
| base64-literal | 2 | 12 | 121 | 112 | 109 | +3 |
| base64-literal | 2 | 12 | 137 | 116 | 109 | +7 |
| base64-literal | 3 | 16 | 121 | 112 | 108 | +4 |
| base64-literal | 3 | 16 | 137 | 115 | 108 | +7 |
| base64-literal | 4 | 20 | 121 | 112 | 107 | +5 |
| base64-literal | 4 | 20 | 137 | 114 | 107 | +7 |
| base64-literal | 5 | 24 | 121 | 112 | 106 | +6 |
| base64-literal | 5 | 24 | 137 | 114 | 106 | +8 |
| base64-literal | 6 | 28 | 121 | 112 | 105 | +7 |
| base64-literal | 6 | 28 | 137 | 113 | 105 | +8 |
| call-identifier | 2 | 12 | 121 | 112 | 109 | +3 |
| call-identifier | 2 | 12 | 137 | 117 | 109 | +8 |
| call-identifier | 3 | 16 | 121 | 112 | 108 | +4 |
| call-identifier | 3 | 16 | 137 | 116 | 108 | +8 |
| call-identifier | 4 | 20 | 121 | 112 | 107 | +5 |
| call-identifier | 4 | 20 | 137 | 116 | 107 | +9 |
| call-identifier | 5 | 24 | 121 | 112 | 106 | +6 |
| call-identifier | 5 | 24 | 137 | 115 | 106 | +9 |
| call-identifier | 6 | 28 | 121 | 112 | 105 | +7 |
| call-identifier | 6 | 28 | 137 | 115 | 105 | +10 |
| cast-call | 2 | 12 | 121 | 112 | 109 | +3 |
| cast-call | 2 | 12 | 137 | 120 | 109 | +11 |
| cast-call | 3 | 16 | 121 | 112 | 108 | +4 |
| cast-call | 3 | 16 | 137 | 120 | 108 | +12 |
| cast-call | 4 | 20 | 121 | 112 | 107 | +5 |
| cast-call | 4 | 20 | 137 | 120 | 107 | +13 |
| cast-call | 5 | 24 | 121 | 112 | 106 | +6 |
| cast-call | 5 | 24 | 137 | 120 | 106 | +14 |
| cast-call | 6 | 28 | 121 | 112 | 105 | +7 |
| cast-call | 6 | 28 | 137 | 120 | 105 | +15 |
| generic-call | 2 | 12 | 121 | 112 | 109 | +3 |
| generic-call | 2 | 12 | 137 | 120 | 109 | +11 |
| generic-call | 3 | 16 | 121 | 112 | 108 | +4 |
| generic-call | 3 | 16 | 137 | 120 | 108 | +12 |
| generic-call | 4 | 20 | 121 | 112 | 107 | +5 |
| generic-call | 4 | 20 | 137 | 120 | 107 | +13 |
| generic-call | 5 | 24 | 121 | 112 | 106 | +6 |
| generic-call | 5 | 24 | 137 | 120 | 106 | +14 |
| generic-call | 6 | 28 | 121 | 112 | 105 | +7 |
| generic-call | 6 | 28 | 137 | 120 | 105 | +15 |
| collection-expression | 2 | 12 | 121 | 112 | 109 | +3 |
| collection-expression ⚠ | 2 | 12 | 137 | 120 | 109 | +11 |
| collection-expression | 3 | 16 | 121 | 112 | 108 | +4 |
| collection-expression ⚠ | 3 | 16 | 137 | 120 | 108 | +12 |
| collection-expression | 4 | 20 | 121 | 112 | 107 | +5 |
| collection-expression ⚠ | 4 | 20 | 137 | 120 | 107 | +13 |
| collection-expression | 5 | 24 | 121 | 112 | 106 | +6 |
| collection-expression ⚠ | 5 | 24 | 137 | 120 | 106 | +14 |
| collection-expression | 6 | 28 | 121 | 112 | 105 | +7 |
| collection-expression ⚠ | 6 | 28 | 137 | 120 | 105 | +15 |
| array-initializer | 2 | 12 | 121 | 105 | 109 | -4 |
| array-initializer | 2 | 12 | 137 | 101 | 109 | -8 |
| array-initializer | 3 | 16 | 121 | 104 | 108 | -4 |
| array-initializer | 3 | 16 | 137 | 100 | 108 | -8 |
| array-initializer | 4 | 20 | 121 | 104 | 107 | -3 |
| array-initializer | 4 | 20 | 137 | 99 | 107 | -8 |
| array-initializer | 5 | 24 | 121 | 103 | 106 | -3 |
| array-initializer | 5 | 24 | 137 | 98 | 106 | -8 |
| array-initializer | 6 | 28 | 121 | 103 | 105 | -2 |
| array-initializer | 6 | 28 | 137 | 97 | 105 | -8 |
| binary-chain | 2 | 12 | 121 | 111 | 109 | +2 |
| binary-chain | 2 | 12 | 137 | 118 | 109 | +9 |
| binary-chain | 3 | 16 | 121 | 111 | 108 | +3 |
| binary-chain | 3 | 16 | 137 | 118 | 108 | +10 |
| binary-chain | 4 | 20 | 121 | 111 | 107 | +4 |
| binary-chain | 4 | 20 | 137 | 118 | 107 | +11 |
| binary-chain | 5 | 24 | 121 | 111 | 106 | +5 |
| binary-chain | 5 | 24 | 137 | 118 | 106 | +12 |
| binary-chain | 6 | 28 | 121 | 111 | 105 | +6 |
| binary-chain | 6 | 28 | 137 | 118 | 105 | +13 |
| ternary | 2 | 12 | 121 | 112 | 109 | +3 |
| ternary | 2 | 12 | 137 | 120 | 109 | +11 |
| ternary | 3 | 16 | 121 | 112 | 108 | +4 |
| ternary | 3 | 16 | 137 | 120 | 108 | +12 |
| ternary | 4 | 20 | 121 | 112 | 107 | +5 |
| ternary | 4 | 20 | 137 | 120 | 107 | +13 |
| ternary | 5 | 24 | 121 | 112 | 106 | +6 |
| ternary | 5 | 24 | 137 | 120 | 106 | +14 |
| ternary | 6 | 28 | 121 | 112 | 105 | +7 |
| ternary | 6 | 28 | 137 | 120 | 105 | +15 |
| lambda-argument | 2 | 12 | 121 | 112 | 109 | +3 |
| lambda-argument | 2 | 12 | 137 | 120 | 109 | +11 |
| lambda-argument | 3 | 16 | 121 | 112 | 108 | +4 |
| lambda-argument | 3 | 16 | 137 | 120 | 108 | +12 |
| lambda-argument | 4 | 20 | 121 | 112 | 107 | +5 |
| lambda-argument | 4 | 20 | 137 | 120 | 107 | +13 |
| lambda-argument | 5 | 24 | 121 | 112 | 106 | +6 |
| lambda-argument | 5 | 24 | 137 | 119 | 106 | +13 |
| lambda-argument | 6 | 28 | 121 | 112 | 105 | +7 |
| lambda-argument | 6 | 28 | 137 | 119 | 105 | +14 |
| member-chain | 2 | 12 | 121 | 107 | 109 | -2 |
| member-chain | 2 | 12 | 137 | 107 | 109 | -2 |
| member-chain | 3 | 16 | 121 | 107 | 108 | -1 |
| member-chain | 3 | 16 | 137 | 107 | 108 | -1 |
| member-chain | 4 | 20 | 121 | 107 | 107 | 0 |
| member-chain | 4 | 20 | 137 | 107 | 107 | 0 |
| member-chain | 5 | 24 | 121 | 107 | 106 | +1 |
| member-chain | 5 | 24 | 137 | 107 | 106 | +1 |
| member-chain | 6 | 28 | 121 | 107 | 105 | +2 |
| member-chain | 6 | 28 | 137 | 107 | 105 | +2 |

## `wrap_before_eq = true`

| shape | depth | column | total | longest `=`-break line | predicted | delta |
|---|---:|---:|---:|---:|---:|---:|
| object-initializer | 2 | 12 | 121 | 108 | 109 | -1 |
| object-initializer | 2 | 12 | 137 | 107 | 109 | -2 |
| object-initializer | 3 | 16 | 121 | 108 | 108 | 0 |
| object-initializer | 3 | 16 | 137 | 106 | 108 | -2 |
| object-initializer | 4 | 20 | 121 | 107 | 107 | 0 |
| object-initializer | 4 | 20 | 137 | 105 | 107 | -2 |
| object-initializer | 5 | 24 | 121 | 107 | 106 | +1 |
| object-initializer | 5 | 24 | 137 | 105 | 106 | -1 |
| object-initializer | 6 | 28 | 121 | 106 | 105 | +1 |
| object-initializer | 6 | 28 | 137 | 104 | 105 | -1 |
| base64-literal | 2 | 12 | 121 | 110 | 109 | +1 |
| base64-literal | 2 | 12 | 137 | 115 | 109 | +6 |
| base64-literal | 3 | 16 | 121 | 110 | 108 | +2 |
| base64-literal | 3 | 16 | 137 | 114 | 108 | +6 |
| base64-literal | 4 | 20 | 121 | 110 | 107 | +3 |
| base64-literal | 4 | 20 | 137 | 114 | 107 | +7 |
| base64-literal | 5 | 24 | 121 | 110 | 106 | +4 |
| base64-literal | 5 | 24 | 137 | 113 | 106 | +7 |
| base64-literal | 6 | 28 | 121 | 110 | 105 | +5 |
| base64-literal | 6 | 28 | 137 | 113 | 105 | +8 |
| call-identifier | 2 | 12 | 121 | 110 | 109 | +1 |
| call-identifier | 2 | 12 | 137 | 116 | 109 | +7 |
| call-identifier | 3 | 16 | 121 | 110 | 108 | +2 |
| call-identifier | 3 | 16 | 137 | 115 | 108 | +7 |
| call-identifier | 4 | 20 | 121 | 110 | 107 | +3 |
| call-identifier | 4 | 20 | 137 | 115 | 107 | +8 |
| call-identifier | 5 | 24 | 121 | 110 | 106 | +4 |
| call-identifier | 5 | 24 | 137 | 114 | 106 | +8 |
| call-identifier | 6 | 28 | 121 | 110 | 105 | +5 |
| call-identifier | 6 | 28 | 137 | 114 | 105 | +9 |
| cast-call | 2 | 12 | 121 | 110 | 109 | +1 |
| cast-call | 2 | 12 | 137 | 118 | 109 | +9 |
| cast-call | 3 | 16 | 121 | 110 | 108 | +2 |
| cast-call | 3 | 16 | 137 | 118 | 108 | +10 |
| cast-call | 4 | 20 | 121 | 110 | 107 | +3 |
| cast-call | 4 | 20 | 137 | 118 | 107 | +11 |
| cast-call | 5 | 24 | 121 | 110 | 106 | +4 |
| cast-call | 5 | 24 | 137 | 118 | 106 | +12 |
| cast-call | 6 | 28 | 121 | 110 | 105 | +5 |
| cast-call | 6 | 28 | 137 | 118 | 105 | +13 |
| generic-call | 2 | 12 | 121 | 110 | 109 | +1 |
| generic-call | 2 | 12 | 137 | 118 | 109 | +9 |
| generic-call | 3 | 16 | 121 | 110 | 108 | +2 |
| generic-call | 3 | 16 | 137 | 118 | 108 | +10 |
| generic-call | 4 | 20 | 121 | 110 | 107 | +3 |
| generic-call | 4 | 20 | 137 | 118 | 107 | +11 |
| generic-call | 5 | 24 | 121 | 110 | 106 | +4 |
| generic-call | 5 | 24 | 137 | 118 | 106 | +12 |
| generic-call | 6 | 28 | 121 | 110 | 105 | +5 |
| generic-call | 6 | 28 | 137 | 118 | 105 | +13 |
| collection-expression | 2 | 12 | 121 | never | 109 | — |
| collection-expression ⚠ | 2 | 12 | 137 | 27 | 109 | -82 |
| collection-expression | 3 | 16 | 121 | never | 108 | — |
| collection-expression ⚠ | 3 | 16 | 137 | 31 | 108 | -77 |
| collection-expression | 4 | 20 | 121 | never | 107 | — |
| collection-expression ⚠ | 4 | 20 | 137 | 35 | 107 | -72 |
| collection-expression | 5 | 24 | 121 | never | 106 | — |
| collection-expression ⚠ | 5 | 24 | 137 | 39 | 106 | -67 |
| collection-expression | 6 | 28 | 121 | never | 105 | — |
| collection-expression ⚠ | 6 | 28 | 137 | 43 | 105 | -62 |
| array-initializer | 2 | 12 | 121 | 104 | 109 | -5 |
| array-initializer | 2 | 12 | 137 | 100 | 109 | -9 |
| array-initializer | 3 | 16 | 121 | 104 | 108 | -4 |
| array-initializer | 3 | 16 | 137 | 100 | 108 | -8 |
| array-initializer | 4 | 20 | 121 | 103 | 107 | -4 |
| array-initializer | 4 | 20 | 137 | 99 | 107 | -8 |
| array-initializer | 5 | 24 | 121 | 102 | 106 | -4 |
| array-initializer | 5 | 24 | 137 | 98 | 106 | -8 |
| array-initializer | 6 | 28 | 121 | 102 | 105 | -3 |
| array-initializer | 6 | 28 | 137 | 97 | 105 | -8 |
| binary-chain | 2 | 12 | 121 | 110 | 109 | +1 |
| binary-chain | 2 | 12 | 137 | 112 | 109 | +3 |
| binary-chain | 3 | 16 | 121 | 110 | 108 | +2 |
| binary-chain | 3 | 16 | 137 | 112 | 108 | +4 |
| binary-chain | 4 | 20 | 121 | 110 | 107 | +3 |
| binary-chain | 4 | 20 | 137 | 112 | 107 | +5 |
| binary-chain | 5 | 24 | 121 | 110 | 106 | +4 |
| binary-chain | 5 | 24 | 137 | 112 | 106 | +6 |
| binary-chain | 6 | 28 | 121 | 110 | 105 | +5 |
| binary-chain | 6 | 28 | 137 | 112 | 105 | +7 |
| ternary | 2 | 12 | 121 | 110 | 109 | +1 |
| ternary | 2 | 12 | 137 | 115 | 109 | +6 |
| ternary | 3 | 16 | 121 | 110 | 108 | +2 |
| ternary | 3 | 16 | 137 | 115 | 108 | +7 |
| ternary | 4 | 20 | 121 | 110 | 107 | +3 |
| ternary | 4 | 20 | 137 | 115 | 107 | +8 |
| ternary | 5 | 24 | 121 | 110 | 106 | +4 |
| ternary | 5 | 24 | 137 | 115 | 106 | +9 |
| ternary | 6 | 28 | 121 | 110 | 105 | +5 |
| ternary | 6 | 28 | 137 | 115 | 105 | +10 |
| lambda-argument | 2 | 12 | 121 | 110 | 109 | +1 |
| lambda-argument | 2 | 12 | 137 | 118 | 109 | +9 |
| lambda-argument | 3 | 16 | 121 | 110 | 108 | +2 |
| lambda-argument | 3 | 16 | 137 | 118 | 108 | +10 |
| lambda-argument | 4 | 20 | 121 | 110 | 107 | +3 |
| lambda-argument | 4 | 20 | 137 | 118 | 107 | +11 |
| lambda-argument | 5 | 24 | 121 | 110 | 106 | +4 |
| lambda-argument | 5 | 24 | 137 | 118 | 106 | +12 |
| lambda-argument | 6 | 28 | 121 | 110 | 105 | +5 |
| lambda-argument | 6 | 28 | 137 | 118 | 105 | +13 |
| member-chain | 2 | 12 | 121 | 106 | 109 | -3 |
| member-chain | 2 | 12 | 137 | 107 | 109 | -2 |
| member-chain | 3 | 16 | 121 | 106 | 108 | -2 |
| member-chain | 3 | 16 | 137 | 107 | 108 | -1 |
| member-chain | 4 | 20 | 121 | 106 | 107 | -1 |
| member-chain | 4 | 20 | 137 | 107 | 107 | 0 |
| member-chain | 5 | 24 | 121 | 106 | 106 | 0 |
| member-chain | 5 | 24 | 137 | 107 | 106 | +1 |
| member-chain | 6 | 28 | 121 | 106 | 105 | +1 |
| member-chain | 6 | 28 | 137 | 107 | 105 | +2 |

