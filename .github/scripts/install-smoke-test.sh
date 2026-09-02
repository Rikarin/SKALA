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

# ⚠ **Two spellings of the feed path, and the difference is what made this test red on Windows and
# only on Windows.** Under Git Bash `pwd` returns an MSYS path — `/d/a/SKALA/SKALA/artifacts/…`.
# MSYS rewrites an argument that looks like one into a Windows path on its way to a native process,
# so `dotnet tool install --add-source /d/a/…` worked and every command below it worked. A path
# *written into a file* gets no such rewriting: NuGet read `/d/a/…` out of the generated
# nuget.config, resolved it as a drive-relative path to `C:\d\a\…`, and failed every restore with
# NU1301. The four MSBuild assertions then failed on a project that had never restored.
#
# So: `$FEED` is for bash, `$FEED_NATIVE` is for anything that lands in a file or is read by a
# native process. On Unix they are the same string and the distinction costs nothing.
FEED_NATIVE="$FEED"
if [ "$WINDOWS" = "1" ] && command -v cygpath >/dev/null 2>&1; then
  FEED_NATIVE="$(cygpath -w "$FEED")"
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
echo "Feed (native): $FEED_NATIVE"
echo "Version: $VERSION"

WORK="$(mktemp -d)"
FAILURES=0

cleanup() {
  dotnet tool uninstall --global Rikarin.Skala.Cli >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

STEP_FAILURES=0
step() {
  echo
  echo "── $* ─────────────────────────────────────────────"
  STEP_FAILURES="$FAILURES"
}

# ⚠ Print the build log, but only when something in this step failed. The four Windows failures
# below were one NU1301 in a log the script had already thrown away, and reading it took a trip to
# the Actions UI. Every build here is under a second and its log is under fifty lines.
dump() {
  if [ "$FAILURES" != "$STEP_FAILURES" ]; then
    echo "  ── the build log ──────────────────────────────"
    sed 's/^/  | /' "$1"
  fi
}

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
    <add key="local-skala" value="$FEED_NATIVE" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML
done

echo "  shallow repository: ${#SHALLOW} characters"
echo "  deep repository:    ${#DEEP} characters"

# ── install ────────────────────────────────────────────────────────────────────
step "dotnet tool install"
dotnet tool install --global --add-source "$FEED_NATIVE" Rikarin.Skala.Cli --version "$VERSION"
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

# ⚠ **Exit 1, and 0 would be the bug.** `verify`'s contract is deliberately stricter than the gate
# it runs — "exit 0 means nothing to do" — and this repository has plenty to do: `Widget.cs` carries
# a planted `SK0240`, and `Gadget.cs` is unarranged (`SK0205`, `SK0210`), because `format` above
# does not arrange. `check --gate=local` passes all of it, a warning being below the local bar.
# ⚠ Asserting 0 asserted that a tree with known work in it is finished, and it was never reachable:
# every `Release` run in the visible history failed at `Pack` on NU5129 before this line ran, for as
# long as the SDK package has carried its generated RuleIds .props. Fixing `Pack` is what surfaced
# it. The findings are planted and stay — `SkalaTreatFindingsAsErrors` below needs `Gadget.cs` to
# still say `== null`, so this repository is deliberately NOT arranged.
run 1 "skala verify (exit 1: work remains)" -- "$SKALA" verify
run 0 "skala explain SK1010"          -- "$SKALA" explain SK1010

# ── the same commands, deep ────────────────────────────────────────────────────
step "the commands, ${#DEEP} characters deep"
cd "$DEEP"
run 0 "skala config sync --apply"     -- "$SKALA" config sync --apply
run 0 "skala format"                  -- "$SKALA" format
run 0 "skala format --check (clean)"  -- "$SKALA" format --check --quiet src/Widget.cs
run 0 "skala check --load loose"      -- "$SKALA" check --load loose

# ⚠ `arrange` was in no smoke test at all — the one command of the four in `verify` that the
# installed tool never ran here. It goes in the deep repository rather than the shallow one on
# purpose: arranging rewrites `Gadget.cs`'s `other == null` to `other is null`, which would delete
# the `SK1010` the `SkalaTreatFindingsAsErrors` assertions need. Nothing downstream reads this copy.
run 0 "skala arrange"                 -- "$SKALA" arrange
run 0 "skala arrange --check (clean)" -- "$SKALA" arrange --check --quiet

# ── the other four packages ────────────────────────────────────────────────────
# ⚠ One PackageReference on the meta package, which is the claim doc 02 makes for it. If the
# analyzers or the build target do not arrive through it, they arrive nowhere.
step "dotnet build with a PackageReference on Rikarin.Skala.Sdk"
cd "$SHALLOW/src"
printf '\n[*.cs]\ndotnet_diagnostic.SK1010.severity = warning\n' >> "$SHALLOW/.editorconfig"
BUILD="$WORK/build.log"

# ⚠ **Restore on its own line, before anything reads a build log.** Every assertion from here down
# looks for a string in a log, and a project that never restored produces a log with no SK1010, no
# Skala diagnostic and no warning — which is indistinguishable from a tool that ran and found
# nothing to say. This is the assertion that tells the two apart, and it is the one that was
# missing: the Windows leg spent a release reporting a single NU1301 as four unrelated failures,
# while `SkalaTreatFindingsAsErrors fails the build` passed on the *restore's* exit 1.
run 0 "dotnet restore resolves both packages from the feed" -- dotnet restore --nologo

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
# ⚠ `off` is not silent, and the assertion is not a typo. Per the .props, SkalaMode's `off` means
# "formatting verification only" — the cheap half, which is the default every consumer gets. The
# silent setting is SkalaEnabled=false, which is asserted separately below.
assert "$(grep -q 'are not formatted' "$BUILD" && echo true || echo false)" \
  "SkalaMode=off warns about formatting"
dump "$BUILD"

# ⚠ Exit 1 is *also* what a build that failed to restore returns, and what a compile error returns.
# Checking only the code is how this assertion stayed green on the Windows leg while the four
# around it went red on one NU1301. The code and the reason are two assertions.
ERRORS="$WORK/errors.log"
STATUS=0
dotnet build --nologo -t:Rebuild -p:SkalaTreatFindingsAsErrors=true > "$ERRORS" 2>&1 || STATUS=$?
assert "$([ "$STATUS" = "1" ] && echo true || echo false)" \
  "SkalaTreatFindingsAsErrors fails the build (exit $STATUS)"
assert "$(grep -q 'error .*Skala:.*are not formatted' "$ERRORS" && echo true || echo false)" \
  "and it fails on the Skala formatting diagnostic rather than on something else"
dump "$ERRORS"
rm -f Rough.cs

step "SkalaMode=check runs the analysis half"
dotnet build --nologo -t:Rebuild -p:SkalaMode=check > "$BUILD" 2>&1 || true
assert "$(grep -q 'error' "$BUILD" && echo false || echo true)" "the build has no errors"
# ⚠ **A green build is not evidence that `check` ran.** The targets downgrade every exit code that
# is not this mode's finding code to a warning ending "The build is not gated on it" — so a check
# that could not find the tool, could not load the workspace, or was handed an option the CLI does
# not have leaves a passing build over an ungated tree. `no errors` cannot tell that apart from a
# clean run. This can, and it is the only thing standing between a fail-open and a green board.
assert "$(grep -q 'could not complete' "$BUILD" && echo false || echo true)" \
  "and the check completed rather than warning that the build is not gated on it"
dump "$BUILD"

step "SkalaEnabled=false is silent"
dotnet build --nologo -t:Rebuild -p:SkalaEnabled=false > "$BUILD" 2>&1 || true
assert "$(grep -q 'Skala: ' "$BUILD" && echo false || echo true)" "no Skala diagnostics"
assert "$(grep -q 'error' "$BUILD" && echo false || echo true)" "and the build still succeeds"
dump "$BUILD"

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
