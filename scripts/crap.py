#!/usr/bin/env python3
"""CRAP metric per method, from a coverlet cobertura report.

    CRAP(m) = complexity(m)^2 * (1 - coverage(m))^3 + complexity(m)

A method with coverage 1 is never worse than its complexity; a method with no
coverage is punished quadratically. Anything over 10 wants splitting, testing, or
both.

    dotnet test tests/Worksheet.Tests/Worksheet.Tests.csproj \
        --collect:"XPlat Code Coverage" --results-directory ./coverage
    python3 scripts/crap.py
"""

from __future__ import annotations

import glob
import sys
import xml.etree.ElementTree as ElementTree

THRESHOLD = 10.0
GENERATED = ("XamlIl", "CompiledAvaloniaXaml", ".g.cs", "AssemblyInfo")

# The composition roots build the parts and contain no behaviour of their own; every part they
# wire is measured where it lives. `App` therefore has no test that could fail for a reason.
COMPOSITION_ROOTS = ("App", "Program")

# Closed dispatch tables: one arm per kind of journal change, and one line of work in each arm. Their
# complexity is the length of that list, not tangled logic, and pulling the list apart hides the
# one thing a reader has to be able to see at once — every kind of change the ledger handles.
# They are still printed, so a table that grows into real logic does not hide here.
DISPATCH_TABLES = {
    "LedgerState.Record": "what each kind of journal change means, one line each",
    "EventCodec.KindOf": "the stored name of every kind of change",
    "EventCodec.Decode": "a reader for every stored name",
    "UndoPlanner.Reverse": "the inverse of every kind of change, one line each",
}


def crap(complexity: int, coverage: float) -> float:
    return complexity**2 * (1.0 - coverage) ** 3 + complexity


def collect(report_path: str, only: str | None) -> dict[str, tuple[float, int, float, str]]:
    """One row per method; a method covered by any report counts as covered."""
    root = ElementTree.parse(report_path).getroot()
    found: dict[str, tuple[float, int, float, str]] = {}

    for cls in root.iter("class"):
        class_name = cls.get("name", "")
        source = cls.get("filename", "")
        short_name = class_name.split(".")[-1]

        if only and not class_name.startswith(only):
            continue
        if any(marker in source for marker in GENERATED) or "XamlIl" in short_name:
            continue
        if short_name in COMPOSITION_ROOTS:
            continue

        for method in cls.iter("method"):
            method_name = method.get("name", "?")

            # The XAML compiler emits members that are not ours to test.
            if method_name.startswith("!"):
                continue

            key = f"{short_name}.{method_name}"
            coverage = float(method.get("line-rate", "0"))
            complexity = int(method.get("complexity", "1"))
            previous = found.get(key)

            if previous is None or coverage > previous[2]:
                found[key] = (crap(complexity, coverage), complexity, coverage, source)

    return found


def report(report_paths: list[str], only: str | None) -> int:
    methods: dict[str, tuple[float, int, float, str]] = {}

    for path in report_paths:
        for key, row in collect(path, only).items():
            previous = methods.get(key)
            if previous is None or row[2] > previous[2]:
                methods[key] = row

    rows = sorted(((score, complexity, coverage, key) for key, (score, complexity, coverage, _) in methods.items()), reverse=True)
    over = [row for row in rows if row[0] > THRESHOLD]
    flagged = [row for row in over if row[3] not in DISPATCH_TABLES]
    tables = [row for row in over if row[3] in DISPATCH_TABLES]
    average = sum(row[0] for row in rows) / len(rows) if rows else 0.0
    uncovered = sum(1 for row in rows if row[2] == 0.0)
    line_rate = sum(row[2] for row in rows) / len(rows) if rows else 0.0

    print(f"reports      {len(report_paths)}")
    print(f"methods      {len(rows)}   uncovered {uncovered}   average CRAP {average:.1f}")
    print(f"covered rate {line_rate:.1%} (method average)")
    print(f"over {THRESHOLD:>3.0f}     {len(flagged)}   named dispatch tables {len(tables)}")
    print()

    for title, group in (("over budget", flagged), ("dispatch tables", tables)):
        if not group:
            continue

        print(f"{title}:")
        print(f"{'CRAP':>7}  {'cplx':>4}  {'cov':>6}  method")

        for score, complexity, coverage, key in group:
            print(f"{score:7.1f}  {complexity:4d}  {coverage:6.0%}  {key}")

            if key in DISPATCH_TABLES:
                print(f"{'':>22}  {DISPATCH_TABLES[key]}")

        print()

    return 1 if flagged else 0


def main(argv: list[str]) -> int:
    pattern = argv[1] if len(argv) > 1 else "coverage/**/coverage.cobertura.xml"
    only = argv[2] if len(argv) > 2 else "Worksheet."
    files = sorted(glob.glob(pattern, recursive=True))

    if not files:
        print(f"no coverage report matched {pattern}", file=sys.stderr)
        return 2

    return report(files, only)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
