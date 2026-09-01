"""Writes the SK2072 fixtures.

The fixtures for an invisible-character rule cannot be written by hand: a positive fixture has to
carry a real byte, and a real zero-width byte is exactly as invisible in the fixture as it is in the
code the rule exists to find. So the bytes are placed here, from a code point named in the source of
this script, and every fixture declares what it carries in a `// contains:` header that
InvisibleCharacterFixtureTests checks against the file's actual bytes.
"""
import os

ROOT = 'Rules/Rikarin.Skala.Rules.Tests/fixtures/SK2072'
U = chr


def write(kind, name, header, body, carries):
    path = os.path.join(ROOT, kind, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    declared = ''.join(f'// contains: U+{ord(c):04X}\n' for c in carries)
    open(path, 'w', encoding='utf-8', newline='\n').write(header + declared + body)
    print(f'{path}: {len(carries)} declared code point(s)')


ZWSP, ZWNJ, ZWJ, RLO, NBSP, BOM, TAB = U(0x200B), U(0x200C), U(0x200D), U(0x202E), U(0xA0), U(0xFEFF), U(9)
IDEOGRAPHIC, THIN = U(0x3000), U(0x2009)

write('positive', 'zero-width-space.cs',
      '// A zero-width space between two words. The value is not what it reads as, in any editor.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      f'    public const string Tenant = "tenant{ZWSP}id";\n}}\n', [ZWSP])

write('positive', 'right-to-left-override.cs',
      '// ⚠ The "Trojan Source" class: the literal reorders how it reads without changing what it\n'
      '// contains, so a review sees one string and the program holds another.\n',
      'namespace Fixtures;\n\nsealed class Paths {\n'
      f'    public const string Upload = "safe{RLO}gnp.exe";\n}}\n', [RLO])

write('positive', 'no-break-space.cs',
      '// Indistinguishable from a space, and not one. A split on U+0020 keeps it in the token.\n',
      'namespace Fixtures;\n\nsealed class Headers {\n'
      f'    public const string Accept = "text/html,{NBSP}application/json";\n}}\n', [NBSP])

write('positive', 'in-an-interpolated-string.cs',
      '// The text part of a non-verbatim interpolated string takes escapes, so the fix applies.\n',
      'namespace Fixtures;\n\nsealed class Report {\n'
      '    public static string Render(int count) =>\n'
      f'        $"loaded {{count}}{ZWJ} items";\n}}\n', [ZWJ])

write('positive', 'in-a-character-literal.cs',
      '// A byte order mark as a char constant, which compares equal to nothing anybody typed.\n',
      'namespace Fixtures;\n\nsealed class Marks {\n'
      f"    public const char Leading = '{BOM}';\n}}\n", [BOM])

write('positive', 'a-tab-and-a-zero-width-joiner-in-one-literal.cs',
      '// Two findings in one literal, and the second is not a duplicate of the first: each\n'
      '// character carries its own edit, so `skala fix` repairs both in one pass.\n',
      'namespace Fixtures;\n\nsealed class Columns {\n'
      f'    public const string Row = "left{TAB}right{ZWNJ}edge";\n}}\n', [TAB, ZWNJ])

write('negative', 'already-escaped.cs',
      '// ⚠ The scan reads the token\'s source spelling, never its value. In the raw text this is\n'
      '// six ASCII characters, so the repair is not reported as the problem.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      '    public const string Tenant = "tenant\\u200Bid";\n'
      '    public const string Newline = "left\\nright";\n}\n', [])

write('negative', 'a-verbatim-literal-has-no-escapes.cs',
      '// A verbatim literal has no escape sequences at all — that is what it is for — so there is\n'
      '// nothing to make explicit and the finding would be one nobody could act on.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      f'    public const string Tenant = @"tenant{ZWSP}id";\n}}\n', [ZWSP])

write('negative', 'a-raw-string-literal-has-no-escapes.cs',
      '// ⚠ A real hole rather than a tidy boundary: a bidirectional override in a raw string is\n'
      '// exactly as dangerous and is not reported, because no escape can be written to repair it.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      f'    public const string Tenant = """tenant{RLO}id""";\n}}\n', [RLO])

write('negative', 'a-utf8-literal-is-a-different-token.cs',
      '// A UTF-8 literal is its own SyntaxKind and takes no escapes either.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      f'    public static System.ReadOnlySpan<byte> Tenant => "tenant{ZWSP}id"u8;\n}}\n', [ZWSP])

write('negative', 'an-ideographic-space-is-visible.cs',
      '// ⚠ U+3000 is the space of Japanese typesetting: visible, intentional and correct. A rule\n'
      '// that reported it would be an opinion about typography.\n',
      'namespace Fixtures;\n\nsealed class Labels {\n'
      f'    public const string Heading = "\\u65E5\\u672C{IDEOGRAPHIC}\\u8A9E";\n}}\n', [IDEOGRAPHIC])

write('negative', 'a-thin-space-is-visible.cs',
      '// U+2009 is narrower than a space and is still a space a reader can see.\n',
      'namespace Fixtures;\n\nsealed class Labels {\n'
      f'    public const string Quantity = "12{THIN}kg";\n}}\n', [THIN])

write('negative', 'a-comment-is-not-a-literal.cs',
      f'// This comment holds a zero-width space here:{ZWSP} and it changes no value at all.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      '    public const string Tenant = "tenant-id";\n}\n', [ZWSP])

write('negative', 'an-identifier-is-not-a-literal.cs',
      '// A soft hyphen is a legal C# identifier character (Unicode Cf). It is a naming question,\n'
      '// not a value that silently differs from what it reads as, and IDE1006 is nearer to it.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      f'    public const string Ten{U(0xAD)}ant = "tenant-id";\n}}\n', [U(0xAD)])

write('negative', 'ordinary-text.cs',
      '// The anti-vacuity fixture: if the rule fired here it would fire on everything.\n',
      'namespace Fixtures;\n\nsealed class Keys {\n'
      '    public const string Tenant = "tenant-id";\n'
      '    public const string Path = "a/b/c";\n'
      "    public const char Slash = '/';\n}\n", [])

write('negative', 'generated.cs',
      '// <auto-generated/>\n'
      '// A generator emits what its input held; the byte is not a decision anybody made here.\n',
      'namespace Fixtures;\n\nsealed class GeneratedKeys {\n'
      f'    public const string Tenant = "tenant{ZWSP}id";\n}}\n', [ZWSP])
