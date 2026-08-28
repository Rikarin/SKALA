#!/usr/bin/env bash
#
# Install the packages from a local feed and use them. docs/plan/11 § "Verified by installing it".
#
# ⚠ **This is the test that a pack log cannot be.** `dotnet pack` succeeding proves a zip is
# well-formed. It does not prove that the tool package's command exists, that the command can find
# the tool behind it, that the analyzer package can be restored at all, or that a .props copied
# verbatim into a package is valid XML — and every one of those has been wrong in this repository,
# each of them invisible from inside it. So this runs outside the checkout, in a directory created
# by `mktemp`, against a `git init` that has never seen Skala.
#
# ⚠ The deep-path case is deliberate and is not padding. It was written for the format daemon — a
# Unix domain socket path caps at 104 bytes (macOS) or 108 (Linux), `<repo>/.skala/daemon.sock`
# exceeded that past about eighty-five characters, and the daemon died there of an unhandled
# exception **with exit code 0** while every later format silently took the cold path. The daemon is
# gone and that particular hazard with it, but CI workspaces, nested monorepos, paths under
# ~/Library and git worktrees all still reach depths where path handling goes wrong, and the case
# costs one `mkdir -p`.
#
# Usage: install-smoke-test.sh <feed-directory>

set -euo pipefail

FEED="${1:?usage: install-smoke-test.sh <feed-directory>}"
FEED="$(cd "$FEED" && pwd)"

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) WINDOWS=1 ;;
  *) WINDOWS=0 ;;
esac

if [ "$WINDOWS" = "1" ]; then
  SKALA="$HOME/.dotnet/tools/skala.exe"
else
  SKALA="$HOME/.dotnet/tools/skala"
fi

# ⚠ Refuse to run if a `skala` is already installed. Uninstalling somebody else's copy at the end
# would be worse than not running, and testing against it would not be testing this feed.
if [ -e "$SKALA" ]; then
  echo "FAIL: $SKALA already exists. This test installs and uninstalls a global tool; it will not"
  echo "      touch one it did not install. Remove it first if it is yours to remove."
  exit 1
fi

VERSION="$(basename "$(ls "$FEED"/Rikarin.Skala.Cli.*.nupkg | head -1)" .nupkg)"
VERSION="${VERSION#Rikarin.Skala.Cli.}"
echo "Feed:    $FEED"
echo "Version: $VERSION"

WORK="$(mktemp -d)"
FAILURES=0

cleanup() {
  dotnet tool uninstall --global Rikarin.Skala.Cli >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

step() { echo; echo "── $* ─────────────────────────────────────────────"; }

# `run <expected-exit> <description> -- command…`
run() {
  local expected="$1" description="$2"; shift 3
  local status=0
  "$@" || status=$?
  if [ "$status" = "$expected" ]; then
    echo "  ok   ($status) $description"
  else
    echo "  FAIL (got $status, wanted $expected) $description"
    FAILURES=$((FAILURES + 1))
  fi
}

assert() {
  if [ "$1" = "true" ]; then
    echo "  ok   $2"
  else
    echo "  FAIL $2"
    FAILURES=$((FAILURES + 1))
  fi
}

# ── the two repositories ───────────────────────────────────────────────────────
SHALLOW="$WORK/shallow"

# ⚠ 100+ characters of nesting before anything under `.skala/` is appended. `mktemp -d` already
# contributes a fair amount on macOS ($TMPDIR is /var/folders/xx/…/T/), which is the point.
DEEP="$WORK/deep/organisation-monorepo/services-and-libraries/backend-platform-core/checkout-experience"

for repository in "$SHALLOW" "$DEEP"; do
  mkdir -p "$repository/src"
  git init -q "$repository"
  cat > "$repository/src/Widget.cs" <<'CSHARP'
namespace Smoke;

public   class Widget
{
        public   int  Id {get;set;}
    public string Name{get;set;}="widget";

    public   int Add( int a,int b ){
        if(a>b){return a+b;}
        else {
            return b-a;
        }
    }
}
CSHARP
  cat > "$repository/src/Gadget.cs" <<'CSHARP'
using System;

namespace Smoke;

public class Gadget {
    public bool Matches(string other) {
        if (other == null) { return false; }
        return true;
    }
}
CSHARP
  cat > "$repository/src/Smoke.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Rikarin.Skala.Sdk" Version="[SKALA_VERSION]" PrivateAssets="all" />
  </ItemGroup>
</Project>
XML
  # ⚠ The bracket is exact-version, and it is the form docs/plan/18 § "What a consumer pins" tells
  # people to use — so it is the form this test proves works. A floating reference resolved from a
  # feed holding exactly one version proves nothing about the reference and everything about the
  # feed. It also exercises the pre-release case: an exact reference is how `2.0.0-alpha.N` is
  # reachable at all without a `--prerelease` somewhere.
  sed -i.bak "s/SKALA_VERSION/$VERSION/" "$repository/src/Smoke.csproj" && rm -f "$repository/src/Smoke.csproj.bak"
  cat > "$repository/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-skala" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML
done

echo "  shallow repository: ${#SHALLOW} characters"
echo "  deep repository:    ${#DEEP} characters"

# ── install ────────────────────────────────────────────────────────────────────
step "dotnet tool install"
dotnet tool install --global --add-source "$FEED" Rikarin.Skala.Cli --version "$VERSION"
assert "$([ -e "$SKALA" ] && echo true || echo false)" "the shim exists at $SKALA"
"$SKALA" --version

# ── the commands ───────────────────────────────────────────────────────────────
step "the commands, in the shallow repository"
cd "$SHALLOW"
run 0 "skala config sync --apply"    -- "$SKALA" config sync --apply
assert "$(grep -q 'skala:canonical begin' .editorconfig && echo true || echo false)" \
  ".editorconfig carries a canonical block"
run 0 "skala config diff --canonical" -- "$SKALA" config diff --canonical
run 0 "skala format"                  -- "$SKALA" format
run 0 "skala format --check (clean)"  -- "$SKALA" format --check --quiet
run 0 "skala check --load loose"      -- "$SKALA" check --load loose
run 0 "skala verify"                  -- "$SKALA" verify
run 0 "skala explain SK1010"          -- "$SKALA" explain SK1010

# ── the same commands, deep ────────────────────────────────────────────────────
step "the commands, ${#DEEP} characters deep"
cd "$DEEP"
run 0 "skala config sync --apply"     -- "$SKALA" config sync --apply
run 0 "skala format"                  -- "$SKALA" format
run 0 "skala format --check (clean)"  -- "$SKALA" format --check --quiet src/Widget.cs
run 0 "skala check --load loose"      -- "$SKALA" check --load loose

# ── the other four packages ────────────────────────────────────────────────────
# ⚠ One PackageReference on the meta package, which is the claim doc 02 makes for it. If the
# analyzers or the build target do not arrive through it, they arrive nowhere.
step "dotnet build with a PackageReference on Rikarin.Skala.Sdk"
cd "$SHALLOW/src"
printf '\n[*.cs]\ndotnet_diagnostic.SK1010.severity = warning\n' >> "$SHALLOW/.editorconfig"
BUILD="$WORK/build.log"
dotnet build --nologo > "$BUILD" 2>&1 || true
cat "$BUILD"
assert "$(grep -q 'warning SK1010' "$BUILD" && echo true || echo false)" \
  "the analyzer package produced SK1010 in a plain dotnet build"
assert "$(grep -q 'error' "$BUILD" && echo false || echo true)" \
  "the build has no errors"

step "the MSBuild target sees an unformatted file"
cat > Rough.cs <<'CSHARP'
namespace Smoke;
public class Rough{public int X{get;set;}}
CSHARP
dotnet build --nologo -t:Rebuild > "$BUILD" 2>&1 || true
assert "$(grep -q 'are not formatted' "$BUILD" && echo true || echo false)" \
  "SkalaMode=off warns about formatting"
run 1 "SkalaTreatFindingsAsErrors fails the build" -- dotnet build --nologo -t:Rebuild -p:SkalaTreatFindingsAsErrors=true
rm -f Rough.cs

step "SkalaMode=check runs the analysis half"
dotnet build --nologo -t:Rebuild -p:SkalaMode=check > "$BUILD" 2>&1 || true
assert "$(grep -q 'error' "$BUILD" && echo false || echo true)" "the build has no errors"

step "SkalaEnabled=false is silent"
dotnet build --nologo -t:Rebuild -p:SkalaEnabled=false > "$BUILD" 2>&1 || true
assert "$(grep -q 'Skala: ' "$BUILD" && echo false || echo true)" "no Skala diagnostics"

# ── uninstall ──────────────────────────────────────────────────────────────────
step "dotnet tool uninstall"
cd "$WORK"
dotnet tool uninstall --global Rikarin.Skala.Cli
assert "$([ ! -e "$SKALA" ] && echo true || echo false)" "the shim is gone"
assert "$([ ! -d "$HOME/.dotnet/tools/.store/rikarin.skala.cli" ] && echo true || echo false)" \
  "the package store entry is gone"
assert "$(dotnet tool list --global | grep -qi 'rikarin.skala' && echo false || echo true)" \
  "no skala in the global tool list"

echo
if [ "$FAILURES" = "0" ]; then
  echo "install smoke test: PASS"
else
  echo "install smoke test: $FAILURES FAILED"
fi
exit "$FAILURES"
