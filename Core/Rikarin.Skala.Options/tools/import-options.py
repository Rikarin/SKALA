#!/usr/bin/env python3
"""
One-off importer that seeds Core/Rikarin.Skala.Options/options.json (docs/plan/15 § M0).

It is committed for provenance, not because it runs in the build: options.json is the source of
truth and is edited by hand from here on. Re-running it would discard tier promotions.

Inputs
------
* ``editor_config_template`` in the repository root — the Rider export, the thing being described.
* JetBrains' published EditorConfig property tables, fetched into a cache directory:

      https://www.jetbrains.com/help/resharper/EditorConfig_Index.html
      https://www.jetbrains.com/help/resharper/EditorConfig_<page>.html

  Those pages are the only authority used for (a) which language a ReSharper property belongs to
  and (b) what its property-name aliases and value domain are.

What the tables do NOT contain
------------------------------
⚠ A default value. JetBrains documents property names, languages and possible values, and never
the shipped default. Every seeded entry therefore carries ``defaultSource: "template"``: the
recorded default is the value the export happens to hold, which is Rider's default for most keys
and the author's choice for the rest, and nothing distinguishes the two. ``skala config distill``
only drops a key whose ``defaultSource`` is ``resharper-docs``, so on this registry it drops
nothing — which is the correct behaviour, not a bug. See the report in docs/plan/03.

Usage
-----
    python3 import-options.py --cache <dir> --repo <repo-root> --out options.json

``--cache`` must contain the JetBrains help pages, downloaded with e.g.

    curl -o EditorConfig_Index.html https://www.jetbrains.com/help/resharper/EditorConfig_Index.html
"""

from __future__ import annotations

import argparse
import collections
import glob
import json
import os
import re

LANG_BY_PAGE_PREFIX = {
    "CSHARP": "csharp",
    "Generalized": "generalized",
    "XMLDOC": "xmldoc",
    "CPP": "cpp",
    "VBASIC": "vb",
    "HTML": "html",
    "XML": "xml",
    "CSS": "css",
    "SHADERLAB": "shaderlab",
    "Razor": "razor",
    "Protobuf": "protobuf",
}

# Languages whose formatting Skala is being built for. Everything else in the export is another
# product's configuration that happens to share the file.
SKALA_LANGUAGES = {"csharp", "xmldoc", "generalized"}

# docs/plan/16 § Q1 and docs/plan/03 § "Four tiers": accepted, parsed, deliberately not implemented.
TIER_C_KEYS = {
    "resharper_old_engine",
    "resharper_use_old_engine",
    "resharper_autodetect_indent_settings",
    "resharper_apply_auto_detected_rules",
    "resharper_use_indent_from_vs",
    "resharper_show_autodetect_configure_formatting_tip",
}

# Enum names for the value domains JetBrains documents. Keyed by the sorted value tuple so that a
# domain gets one name however many properties share it.
ENUM_NAMES = {
    ("chop_always", "chop_if_long", "wrap_if_long"): "WrapStyle",
    ("chop_if_long", "wrap_if_long"): "SimpleWrapStyle",
    ("chop_if_long", "no_wrap", "wrap_if_long"): "VerbatimWrapStyle",
    ("always", "false", "if_owner_is_single_line", "never", "true"): "PlacementStyle",
    (
        "end_of_line",
        "end_of_line_no_space",
        "next_line",
        "next_line_shifted",
        "next_line_shifted_2",
        "pico",
    ): "BraceStyle",
    ("not_required", "required", "required_for_multiline", "required_for_multiline_statement"): "BraceRequirement",
    (
        "not_required",
        "not_required_for_both",
        "required",
        "required_for_multiline",
        "required_for_multiline_statement",
    ): "BraceRequirementWithBoth",
    ("inside", "none", "outside", "outside_and_inside"): "ParenthesesIndentStyle",
    ("named", "positional"): "ArgumentStyle",
    ("use_explicit_type", "use_var", "use_var_when_evident"): "VarStyle",
    ("block_body", "expression_body"): "BodyStyle",
    ("accessors_with_block_body", "accessors_with_expression_body", "expression_body"): "AccessorOwnerBodyStyle",
    ("do_not_change", "no_indent", "outdent", "usual_indent"): "PreprocessorIndentStyle",
    ("all", "event", "field", "method", "none", "property"): "MemberKind",
    ("use_clr_name", "use_keyword"): "BuiltInTypeStyle",
    ("explicit", "implicit"): "ExplicitnessStyle",
    ("explicitly_typed", "target_typed"): "ObjectCreationStyle",
    ("default_expression", "default_literal"): "DefaultValueStyle",
    ("space", "tab"): "IndentStyle",
    ("optimal_fill", "use_spaces", "use_tabs_only"): "TabFillStyle",
    (
        "do_not_touch",
        "first_attribute_on_single_line",
        "on_different_lines",
        "on_single_line",
    ): "AttributeArrangementStyle",
    ("align_by_first_attribute", "double_indent", "single_indent"): "AttributeIndentStyle",
    (
        "DoNotTouch",
        "OneIndent",
        "RemoveIndent",
        "ZeroIndent",
        "do_not_touch",
        "one_indent",
        "remove_indent",
        "zero_indent",
    ): "ChildIndentStyle",
    ("multiline", "together", "together_same_line"): "EmptyBlockStyle",
    ("base_class", "this_class"): "QualifyDeclaredIn",
    ("current_type", "declared_type"): "QualifyWith",
    ("remove", "remove_if_not_clarifies_precedence"): "ParenthesesRedundancyStyle",
    ("block_scoped", "file_scoped"): "NamespaceDeclarationStyle",
    ("join", "separate"): "AttributeSectionStyle",
    ("empty_recursive_pattern", "not_null_pattern"): "NullCheckingPatternStyle",
    ("align", "do_not_change", "indent"): "RawStringIndentStyle",
    ("autodetect", "compact", "expanded", "simple_wrap"): "NestedTernaryStyle",
    ("leave_all", "leave_multiple", "leave_tabs", "remove_all"): "ExtraSpacesStyle",
}

# ReSharper's value aliases (docs/plan/03 § "The option registry"). Written into the registry so the
# generated parser accepts both spellings.
ENUM_VALUE_ALIASES = {
    "PlacementStyle": {"true": "always", "false": "never"},
    "ChildIndentStyle": {
        "DoNotTouch": "do_not_touch",
        "OneIndent": "one_indent",
        "ZeroIndent": "zero_indent",
        "RemoveIndent": "remove_indent",
    },
}

# Options that are not in JetBrains' ReSharper tables but that Skala must know about: the standard
# EditorConfig keys and the Microsoft .NET code-style keys the export also carries. Values are
# (type, construct, summary, flags, severitySuffix).
STANDARD_OPTIONS = {
    "charset": ("string", "File", "The file's character encoding."),
    "end_of_line": ("enum:LineEnding", "File", "The line ending written by the formatter."),
    "trim_trailing_whitespace": ("bool", "File", "Whether trailing whitespace is removed from every line."),
    "max_line_length": (
        "int",
        "File",
        "The standard EditorConfig column limit. ReSharper reads resharper_csharp_max_line_length "
        "instead; when both exist the ReSharper key wins and SK9005 reports the disagreement.",
    ),
    "file_header_template": ("string", "File", "The header comment inserted at the top of a new file."),
}

MICROSOFT_OPTIONS: dict[str, tuple[str, str, str, bool]] = {
    "csharp_indent_braces": ("bool", "Block", "Indent the braces of a block themselves.", False),
    "csharp_new_line_before_members_in_object_initializers": (
        "bool",
        "ObjectInitializer",
        "Put each member of an object initializer on its own line.",
        False,
    ),
    "csharp_new_line_before_open_brace": ("string", "Braces", "Which constructs put '{' on a new line.", False),
    "csharp_new_line_between_query_expression_clauses": (
        "bool",
        "QueryExpression",
        "Put each LINQ query clause on its own line.",
        False,
    ),
    "csharp_preferred_modifier_order": ("string", "ModifierList", "The order modifiers are written in.", True),
    "csharp_prefer_braces": ("enum:BraceRequirement", "EmbeddedStatement", "Whether braces are required.", True),
    "csharp_preserve_single_line_blocks": ("bool", "Block", "Keep a block that was written on one line on one line.", False),
    "csharp_space_after_dot": ("bool", "MemberAccess", "Space after '.'.", False),
    "csharp_space_around_binary_operators": ("string", "BinaryExpression", "Spacing around binary operators.", False),
    "csharp_space_before_dot": ("bool", "MemberAccess", "Space before '.'.", False),
    "csharp_space_between_parentheses": ("string", "Parentheses", "Spacing inside parentheses.", False),
    "csharp_style_namespace_declarations": (
        "enum:NamespaceDeclarationStyle",
        "NamespaceDeclaration",
        "File-scoped or block-scoped namespaces.",
        True,
    ),
    "csharp_style_prefer_utf8_string_literals": ("bool", "Literal", "Prefer u8 string literals.", True),
    "csharp_style_var_elsewhere": ("bool", "LocalDeclaration", "Use 'var' where the type is not apparent.", True),
    "csharp_style_var_for_built_in_types": ("bool", "LocalDeclaration", "Use 'var' for built-in types.", True),
    "csharp_style_var_when_type_is_apparent": ("bool", "LocalDeclaration", "Use 'var' where the type is apparent.", True),
    "csharp_using_directive_placement": ("string", "UsingDirective", "Usings inside or outside the namespace.", True),
    "dotnet_separate_import_directive_groups": ("bool", "UsingDirective", "Blank line between using groups.", False),
    "dotnet_style_parentheses_in_arithmetic_binary_operators": (
        "string",
        "BinaryExpression",
        "Parentheses in arithmetic expressions.",
        True,
    ),
    "dotnet_style_parentheses_in_other_binary_operators": (
        "string",
        "BinaryExpression",
        "Parentheses in other binary expressions.",
        True,
    ),
    "dotnet_style_parentheses_in_relational_binary_operators": (
        "string",
        "BinaryExpression",
        "Parentheses in relational expressions.",
        True,
    ),
    "dotnet_style_predefined_type_for_locals_parameters_members": (
        "bool",
        "TypeReference",
        "Use the language keyword for a predefined type in declarations.",
        True,
    ),
    "dotnet_style_predefined_type_for_member_access": (
        "bool",
        "TypeReference",
        "Use the language keyword for a predefined type in member access.",
        True,
    ),
    "dotnet_style_prefer_collection_expression": ("string", "CollectionExpression", "Prefer collection expressions.", True),
    "dotnet_style_qualification_for_event": ("bool", "MemberAccess", "Qualify event access with 'this'.", True),
    "dotnet_style_qualification_for_field": ("bool", "MemberAccess", "Qualify field access with 'this'.", True),
    "dotnet_style_qualification_for_method": ("bool", "MemberAccess", "Qualify method access with 'this'.", True),
    "dotnet_style_qualification_for_property": ("bool", "MemberAccess", "Qualify property access with 'this'.", True),
    "dotnet_style_require_accessibility_modifiers": (
        "string",
        "ModifierList",
        "When an explicit accessibility modifier is required.",
        True,
    ),
}

# ReSharper properties the export sets that JetBrains' tables do not document, and that are
# nonetheless C#-relevant. Judged by vocabulary; anything whose vocabulary is C++, VB, F#, HTML,
# XML, Razor, Slate or Unreal is left out of the registry entirely and reported as SK9001.
UNDOCUMENTED_CSHARP_KEYS = {
    "resharper_align_multiline_type_parameter_constraints": ("bool", "TypeParameterConstraintClause"),
    "resharper_align_multiline_type_parameter_list": ("bool", "TypeParameterList"),
    "resharper_apply_on_completion": ("bool", "Editor"),
    "resharper_configure_await_analysis_mode": ("string", "AwaitExpression"),
    "resharper_csharp_keep_nontrivial_alias": ("bool", "UsingDirective"),
    "resharper_csharp_wrap_lines": ("bool", "Wrapping"),
    "resharper_declaration_body_on_the_same_line": ("enum:PlacementStyle", "DeclarationBody"),
    "resharper_default_exception_variable_name": ("string", "CatchClause"),
    "resharper_disable_blank_line_changes": ("bool", "Formatter"),
    "resharper_disable_formatter": ("bool", "Formatter"),
    "resharper_disable_indenter": ("bool", "Formatter"),
    "resharper_disable_int_align": ("bool", "Formatter"),
    "resharper_disable_line_break_changes": ("bool", "Formatter"),
    "resharper_disable_line_break_removal": ("bool", "Formatter"),
    "resharper_disable_space_changes": ("bool", "Formatter"),
    "resharper_disable_space_changes_before_trailing_comment": ("bool", "Formatter"),
    "resharper_dont_remove_extra_blank_lines": ("bool", "BlankLines"),
    "resharper_empty_string": ("string", "Literal"),
    "resharper_enforce_line_ending_style": ("bool", "File"),
    "resharper_event_handler_pattern_long": ("string", "EventHandler"),
    "resharper_event_handler_pattern_short": ("string", "EventHandler"),
    "resharper_expression_pars": ("enum:ParenthesesIndentStyle", "Parentheses"),
    "resharper_formatter_off_tag": ("string", "Formatter"),
    "resharper_formatter_on_tag": ("string", "Formatter"),
    "resharper_formatter_tags_accept_regexp": ("bool", "Formatter"),
    "resharper_formatter_tags_enabled": ("bool", "Formatter"),
    "resharper_ignore_space_preservation": ("bool", "Formatter"),
    "resharper_indent_break_from_case": ("bool", "SwitchSection"),
    "resharper_keep_existing_lambda_and_anonymous_function_parens_arrangement": ("bool", "Lambda"),
    "resharper_keep_existing_line_break_before_declaration_body": ("bool", "DeclarationBody"),
    "resharper_keep_user_wrapping": ("bool", "Wrapping"),
    "resharper_labeled_statement_style": ("string", "LabeledStatement"),
    "resharper_max_lambda_and_anonymous_function_parameters_on_line": ("int", "Lambda"),
    "resharper_nullable_enable_for_new_files": ("bool", "File"),
    "resharper_outdent_ternary_ops": ("bool", "ConditionalExpression"),
    "resharper_parentheses_non_obvious_operations": ("string", "BinaryExpression"),
    "resharper_parentheses_same_type_operations": ("bool", "BinaryExpression"),
    "resharper_place_primary_constructor_initializer_on_same_line": ("bool", "PrimaryConstructor"),
    "resharper_place_single_method_argument_lambda_on_same_line": ("bool", "Lambda"),
    "resharper_prefer_line_break_after_multiline_lparen": ("bool", "Wrapping"),
    "resharper_prefer_roslyn_rules_for_parentheses_clarity": ("bool", "BinaryExpression"),
    "resharper_remove_only_unused_aliases": ("bool", "UsingDirective"),
    "resharper_remove_spaces_on_blank_lines": ("bool", "File"),
    "resharper_remove_this_qualifier": ("bool", "MemberAccess"),
    "resharper_remove_unused_only_aliases": ("bool", "UsingDirective"),
    "resharper_sort_usings": ("bool", "UsingDirective"),
    "resharper_space_after_triple_slash": ("bool", "XmlDocComment"),
    "resharper_space_before_colon_in_ctor_initializer": ("bool", "ConstructorInitializer"),
    "resharper_space_before_trailing_comment_text": ("bool", "Comment"),
    "resharper_space_within_spread_pattern": ("bool", "ListPattern"),
    "resharper_support_vs_event_naming_pattern": ("bool", "EventHandler"),
    "resharper_treat_case_statement_with_break_as_simple": ("bool", "SwitchSection"),
    "resharper_use_indent_from_vs": ("bool", "Indentation"),
    "resharper_use_indents_from_main_language_in_file": ("bool", "Indentation"),
    "resharper_use_old_engine": ("bool", "Formatter"),
    "resharper_wrap_after_binary_opsign": ("bool", "BinaryExpression"),
    "resharper_wrap_after_dot": ("bool", "MemberAccess"),
    "resharper_wrap_after_lambda_and_anonymous_function_declaration_lpar": ("bool", "Lambda"),
    "resharper_wrap_before_lambda_and_anonymous_function_declaration_lpar": ("bool", "Lambda"),
    "resharper_wrap_before_lambda_and_anonymous_function_declaration_rpar": ("bool", "Lambda"),
    "resharper_wrap_comments": ("bool", "Comment"),
    "resharper_wrap_lambda_and_anonymous_function_parameters_style": ("enum:SimpleWrapStyle", "Lambda"),
    "resharper_show_autodetect_configure_formatting_tip": ("bool", "Editor"),
    "resharper_apply_auto_detected_rules": ("bool", "Indentation"),
    "resharper_autodetect_indent_settings": ("bool", "Indentation"),
    "resharper_xmldoc_insert_final_newline": ("bool", "XmlDocComment"),
    "resharper_xmldoc_wrap_lines": ("bool", "XmlDocComment"),
}

LINE_ENDING_VALUES = ["lf", "crlf", "cr"]


def unescape(text: str) -> str:
    for a, b in (
        ("&amp;", "&"),
        ("&lt;", "<"),
        ("&gt;", ">"),
        ("&#39;", "'"),
        ("&quot;", '"'),
        ("&nbsp;", " "),
    ):
        text = text.replace(a, b)
    return text


def strip_tags(text: str) -> str:
    return unescape(re.sub("<[^>]+>", "", text)).strip()


def split_sections(html: str, level: str):
    """Yield (anchor, title html, body html, enclosing h2 heading) for every h<level> section."""
    headings = [(m.start(), strip_tags(m.group(1))) for m in re.finditer(r"<h2 id=\"[^\"]*\"[^>]*>(.*?)</h2>", html, re.S)]

    def enclosing(position: int) -> str:
        current = ""
        for start, text in headings:
            if start > position:
                break
            current = text
        return current

    pattern = re.compile(r'<h%s id="([^"]+)"[^>]*>(.*?)</h%s>' % (level, level), re.S)
    matches = list(pattern.finditer(html))
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(html)
        yield match.group(1), match.group(2), html[match.end() : end], enclosing(match.start())


def read_jetbrains_pages(cache: str) -> list[dict]:
    props: list[dict] = []
    for path in sorted(glob.glob(os.path.join(cache, "EditorConfig_*.html"))):
        name = os.path.basename(path)
        if name == "EditorConfig_Index.html":
            continue
        prefix = name[len("EditorConfig_") :].split("_")[0].replace(".html", "")
        lang = LANG_BY_PAGE_PREFIX.get(prefix, "other")
        html = open(path, encoding="utf-8", errors="replace").read()
        sections = list(split_sections(html, "3"))
        if "Generalized" in name:
            sections += list(split_sections(html, "2"))
        for anchor, title_html, body, group in sections:
            names_match = re.search(r"Property names?:</h4>(.*?)</section>", body, re.S)
            if not names_match:
                continue
            names = [unescape(x).strip() for x in re.findall(r"<code[^>]*>([^<]+)</code>", names_match.group(1))]
            lang_aliases_match = re.search(r"Language-specific aliases:</h4>(.*?)</section>", body, re.S)
            lang_aliases = (
                [unescape(x).strip() for x in re.findall(r"<code[^>]*>([^<]+)</code>", lang_aliases_match.group(1))]
                if lang_aliases_match
                else []
            )
            expands_match = re.search(r"Allows setting the following properties:</h4>(.*?)</section>", body, re.S)
            expands = (
                [unescape(x).strip() for x in re.findall(r"<code[^>]*>([^<]+)</code>", expands_match.group(1))]
                if expands_match
                else []
            )
            values_match = re.search(r"Possible values?:</h4>(.*?)</section>", body, re.S)
            value_lines: list[str] = []
            if values_match:
                fragment = re.sub(r"<(li|p|br|/p|/li)[^>]*>", "\n", values_match.group(1))
                value_lines = [line.strip() for line in strip_tags(fragment).split("\n") if line.strip()]
            props.append(
                {
                    "anchor": anchor,
                    "title": strip_tags(title_html),
                    "group": group,
                    "names": names,
                    "langAliases": lang_aliases,
                    "expands": expands,
                    "valueLines": value_lines,
                    "page": name,
                    "lang": lang,
                }
            )
    return props


def expand_form(name: str) -> list[str]:
    name = name.strip()
    if name.startswith("[resharper_]"):
        base = name[len("[resharper_]") :]
        return [base, "resharper_" + base]
    return [name]


def read_template(repo: str) -> list[tuple[int, str, str, str]]:
    path = os.path.join(repo, "editor_config_template")
    entries = []
    section = "(preamble)"
    for number, raw in enumerate(open(path, encoding="utf-8").read().split("\n"), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.startswith(";"):
            continue
        if line.startswith("["):
            section = line
            continue
        if "=" in line:
            key, value = line.split("=", 1)
            entries.append((number, section, key.strip(), value.strip()))
    return entries


SPECIFICITY_PREFIXES = ("resharper_csharp_", "resharper_xmldoc_", "resharper_", "csharp_", "dotnet_")


def specificity(form: str) -> int:
    """Lower is more specific. docs/plan/03 § "Precedence" step 3."""
    for index, prefix in enumerate(SPECIFICITY_PREFIXES):
        if form.startswith(prefix):
            return index
    return len(SPECIFICITY_PREFIXES)


def classify_value(value_lines: list[str]) -> tuple[str, list[dict] | None]:
    if value_lines == ["true | false"]:
        return "bool", None
    if len(value_lines) == 1 and "integer" in value_lines[0]:
        return "int", None
    members = []
    for line in value_lines:
        name, _, summary = line.partition(":")
        members.append({"name": name.strip(), "summary": summary.strip()})
    if not members:
        return "string", None
    key = tuple(sorted(m["name"] for m in members))
    name = ENUM_NAMES.get(key)
    if name is None:
        return "string", None
    return "enum:" + name, members


def pascal(text: str) -> str:
    words = re.findall(r"[A-Za-z0-9']+", text)
    return "".join(w[0].upper() + w[1:] for w in words if w).replace("'", "") or "Other"


def build(repo: str, cache: str) -> dict:
    props = read_jetbrains_pages(cache)
    template = read_template(repo)
    template_values: dict[str, tuple[str, int]] = {}
    for number, _section, key, value in template:
        template_values.setdefault(key, (value, number))

    forms_by_prop: list[set[str]] = []
    for prop in props:
        forms: set[str] = set()
        for name in prop["names"] + prop["langAliases"]:
            forms.update(expand_form(name))
        forms_by_prop.append(forms)

    # A form that reaches more than one *registry-eligible* property is one of ReSharper's
    # generalized properties. It is registered as its own entry with an `expands` list rather than
    # as an alias, so that every alias in the registry maps to exactly one option (SK9004).
    #
    # A form shared with a language Skala does not implement — `resharper_wrap_arguments_style` is
    # both the C# and the VB spelling — is not generalized: there is one C# option behind it, and
    # dropping the alias would make the language-generic spelling an unknown key.
    form_owners: dict[str, list[int]] = collections.defaultdict(list)
    for index, forms in enumerate(forms_by_prop):
        if props[index]["lang"] not in SKALA_LANGUAGES:
            continue
        for form in forms:
            form_owners[form].append(index)

    enums: dict[str, dict] = {}
    options: list[dict] = []
    seen_keys: set[str] = set()

    def register_enum(type_name: str, members: list[dict] | None) -> None:
        if not type_name.startswith("enum:") or members is None:
            return
        name = type_name[len("enum:") :]
        if name in enums:
            return
        enums[name] = {
            "values": members,
            "valueAliases": ENUM_VALUE_ALIASES.get(name, {}),
        }

    def add(entry: dict) -> None:
        if entry["key"] in seen_keys:
            return
        seen_keys.add(entry["key"])
        options.append(entry)

    for index, prop in enumerate(props):
        if prop["lang"] not in SKALA_LANGUAGES:
            continue
        forms = forms_by_prop[index]
        own_forms = sorted(f for f in forms if len(form_owners.get(f, ())) == 1)
        present = [f for f in forms if f in template_values]
        if not present:
            continue
        candidates = own_forms or sorted(forms)
        canonical = min(candidates, key=lambda f: (specificity(f), -len(f), f))
        aliases = [f for f in own_forms if f != canonical]
        winning = min(present, key=lambda f: (specificity(f), -len(f), f))
        value, line = template_values[winning]
        option_type, members = classify_value(prop["valueLines"])
        register_enum(option_type, members)
        expands = sorted({e for name in prop["expands"] for e in expand_form(name)}) if prop["expands"] else []
        add(
            {
                "key": canonical,
                "aliases": aliases,
                "language": "any" if prop["lang"] == "generalized" else prop["lang"],
                "type": option_type,
                "default": value,
                "defaultSource": "template",
                "tier": "C" if canonical in TIER_C_KEYS else "D",
                "construct": pascal(prop["group"]) if prop["group"] else "Other",
                "summary": f"{prop['group']} — {prop['title']}" if prop["group"] else prop["title"],
                "since": "0.1",
                "oracle": None,
                "docs": f"https://www.jetbrains.com/help/resharper/{prop['page']}#{prop['anchor']}",
                "templateLine": line,
                "expands": expands,
            }
        )

    # Generalized forms that own more than one property.
    for form, owners in sorted(form_owners.items()):
        if len(owners) < 2 or form not in template_values or form in seen_keys:
            continue
        targets = [props[o] for o in owners]
        value, line = template_values[form]
        option_type, members = classify_value(targets[0]["valueLines"])
        register_enum(option_type, members)
        expanded = []
        for owner in owners:
            if props[owner]["lang"] not in SKALA_LANGUAGES:
                continue
            own_forms = sorted(f for f in forms_by_prop[owner] if len(form_owners.get(f, ())) == 1)
            if own_forms:
                expanded.append(min(own_forms, key=lambda f: (specificity(f), -len(f), f)))
        add(
            {
                "key": form,
                "aliases": [],
                "language": "any",
                "type": option_type,
                "default": value,
                "defaultSource": "template",
                "tier": "D",
                "construct": "Generalized",
                "summary": "Generalized ReSharper property; sets every option it expands to at once.",
                "since": "0.1",
                "oracle": None,
                "docs": "https://www.jetbrains.com/help/resharper/EditorConfig_Generalized.html",
                "templateLine": line,
                "expands": sorted(set(expanded)),
            }
        )

    for key, (option_type, construct, summary) in STANDARD_OPTIONS.items():
        if option_type == "enum:LineEnding":
            register_enum(option_type, [{"name": v, "summary": ""} for v in LINE_ENDING_VALUES])
        value, line = template_values.get(key, (None, None))
        add(
            {
                "key": key,
                "aliases": [],
                "language": "any",
                "type": option_type,
                "default": value,
                "defaultSource": "unknown",
                "tier": "D",
                "construct": construct,
                "summary": summary,
                "since": "0.1",
                "oracle": None,
                "docs": "https://spec.editorconfig.org/",
                "templateLine": line,
                "expands": [],
            }
        )

    for key, (option_type, construct, summary, severity_suffix) in MICROSOFT_OPTIONS.items():
        if key not in template_values:
            continue
        value, line = template_values[key]
        add(
            {
                "key": key,
                "aliases": [],
                "language": "csharp",
                "type": option_type,
                "default": value,
                "defaultSource": "unknown",
                "tier": "D",
                "construct": construct,
                "summary": summary,
                "since": "0.1",
                "oracle": None,
                "docs": "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options",
                "templateLine": line,
                "severitySuffix": severity_suffix,
                "expands": [],
            }
        )

    for key, (option_type, construct) in UNDOCUMENTED_CSHARP_KEYS.items():
        if key not in template_values or key in seen_keys:
            continue
        value, line = template_values[key]
        add(
            {
                "key": key,
                "aliases": [],
                "language": "xmldoc" if "_xmldoc_" in key else "csharp",
                "type": option_type,
                "default": value,
                "defaultSource": "unknown",
                "tier": "C" if key in TIER_C_KEYS else "D",
                "construct": construct,
                "summary": "Undocumented ReSharper property; classified as C#-relevant by vocabulary.",
                "since": "0.1",
                "oracle": None,
                "docs": None,
                "templateLine": line,
                "expands": [],
            }
        )

    options.sort(key=lambda o: o["key"])
    return {
        "$schema": "./options.schema.json",
        "version": 1,
        "generator": "Core/Rikarin.Skala.Options/tools/import-options.py",
        "notes": [
            "Seeded from editor_config_template. One entry per option the export actually sets.",
            "defaultSource is never 'resharper-docs' in this seed: JetBrains' EditorConfig property "
            "tables document names, languages and possible values, and never a default. "
            "`skala config distill` drops a key only when defaultSource == 'resharper-docs' and the "
            "value equals the default, so on this registry it drops nothing. That is deliberate: a "
            "distill that removes a key on a guessed default silently changes formatting.",
            "Nearly every entry is Tier D. Tier D means 'not implemented yet', which is the honest "
            "state of the formatter at M0, not a defect in the registry.",
        ],
        "enums": dict(sorted(enums.items())),
        "options": options,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=os.getcwd())
    parser.add_argument("--cache", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    registry = build(args.repo, args.cache)
    with open(args.out, "w", encoding="utf-8") as handle:
        json.dump(registry, handle, indent=2)
        handle.write("\n")
    tiers = collections.Counter(o["tier"] for o in registry["options"])
    sources = collections.Counter(o["defaultSource"] for o in registry["options"])
    print(f"{len(registry['options'])} options, {len(registry['enums'])} enums")
    print("tier:", dict(tiers))
    print("defaultSource:", dict(sources))


if __name__ == "__main__":
    main()
