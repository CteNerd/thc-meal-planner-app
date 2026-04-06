#!/usr/bin/env python3

import argparse
import glob
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


def normalize_path(path: str) -> str:
    return (path or "").replace("\\", "/").lstrip("./")


def append_step_summary(markdown: str) -> None:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return

    with open(summary_path, "a", encoding="utf-8") as handle:
        handle.write(markdown)
        if not markdown.endswith("\n"):
            handle.write("\n")


def run_git(args: list[str], check: bool = True) -> str:
    result = subprocess.run(["git", *args], check=check, capture_output=True, text=True)
    return result.stdout


def get_changed_paths(base_ref: str | None, event_name: str) -> list[str]:
    changed_paths: list[str] = []

    if event_name == "pull_request" and base_ref:
        subprocess.run(
            ["git", "fetch", "--no-tags", "origin", base_ref],
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            text=True,
        )

        diff_range = f"origin/{base_ref}...HEAD"
        try:
            output = run_git(["diff", "--name-only", diff_range])
            changed_paths = [normalize_path(line) for line in output.splitlines() if line.strip()]
        except subprocess.CalledProcessError:
            changed_paths = []

    if changed_paths:
        return changed_paths

    try:
        output = run_git(["show", "--pretty=", "--name-only", "HEAD"])
        return [normalize_path(line) for line in output.splitlines() if line.strip()]
    except subprocess.CalledProcessError:
        return []


def pct(covered: int, valid: int) -> float:
    return (covered / valid * 100.0) if valid else 0.0


def parse_condition_coverage(value: str | None) -> tuple[int, int]:
    if not value:
        return 0, 0

    match = re.search(r"\((\d+)/(\d+)\)", value)
    if not match:
        return 0, 0

    return int(match.group(1)), int(match.group(2))


def find_latest_report(pattern: str) -> str:
    files = glob.glob(pattern, recursive=True)
    if not files:
        raise SystemExit(f"No coverage report found for pattern: {pattern}")
    return max(files, key=os.path.getmtime)


def backend_command(args: argparse.Namespace) -> int:
    report_path = args.report or find_latest_report("backend/TestResults/**/coverage.cobertura.xml")
    root = ET.parse(report_path).getroot()

    file_lines: dict[str, list[int]] = defaultdict(list)
    file_branches: dict[str, list[tuple[int, int]]] = defaultdict(list)

    for pkg in root.findall("./packages/package"):
        pkg_name = pkg.attrib.get("name", "")
        for cls in pkg.findall("./classes/class"):
            class_name = cls.attrib.get("name", "")
            filename = normalize_path(cls.attrib.get("filename", ""))
            if "/obj/" in filename:
                continue
            if not (
                pkg_name == "ThcMealPlanner.Api"
                or pkg_name.startswith("ThcMealPlanner.Api.")
                or class_name.startswith("ThcMealPlanner.Api")
                or filename.startswith("ThcMealPlanner.Api/")
                or "/ThcMealPlanner.Api/" in filename
            ):
                continue

            for line in cls.findall("./lines/line"):
                hits = int(line.attrib.get("hits", "0"))
                file_lines[filename].append(hits)
                covered_branches, valid_branches = parse_condition_coverage(line.attrib.get("condition-coverage"))
                if valid_branches:
                    file_branches[filename].append((covered_branches, valid_branches))

    total_line_valid = total_line_covered = 0
    total_branch_valid = total_branch_covered = 0
    rows = []

    for filename, hits in file_lines.items():
        line_valid = len(hits)
        line_covered = sum(1 for hit in hits if hit > 0)
        branch_pairs = file_branches.get(filename, [])
        branch_covered = sum(pair[0] for pair in branch_pairs)
        branch_valid = sum(pair[1] for pair in branch_pairs)
        total_line_valid += line_valid
        total_line_covered += line_covered
        total_branch_valid += branch_valid
        total_branch_covered += branch_covered
        rows.append(
            {
                "filename": filename,
                "line_pct": pct(line_covered, line_valid),
                "line_covered": line_covered,
                "line_valid": line_valid,
                "branch_pct": pct(branch_covered, branch_valid),
                "branch_covered": branch_covered,
                "branch_valid": branch_valid,
            }
        )

    overall_line_pct = pct(total_line_covered, total_line_valid)
    overall_branch_pct = pct(total_branch_covered, total_branch_valid)
    warnings: list[str] = []
    violations: list[str] = []

    if overall_line_pct < args.line_min:
        violations.append(f"Backend API line coverage below minimum: {overall_line_pct:.2f}% < {args.line_min:.2f}%")
    elif overall_line_pct < args.line_target:
        warnings.append(f"Backend API line coverage below target: {overall_line_pct:.2f}% < {args.line_target:.2f}%")

    if total_branch_valid > 0:
        if overall_branch_pct < args.branch_min:
            violations.append(f"Backend API branch coverage below minimum: {overall_branch_pct:.2f}% < {args.branch_min:.2f}%")
        elif overall_branch_pct < args.branch_target:
            warnings.append(f"Backend API branch coverage below target: {overall_branch_pct:.2f}% < {args.branch_target:.2f}%")

    changed_file_results = []
    if args.event_name == "pull_request":
        changed_paths = get_changed_paths(args.base_ref, args.event_name)
        changed_api_files = []
        for path in changed_paths:
            normalized = normalize_path(path)
            if normalized.startswith("backend/ThcMealPlanner.Api/") and normalized.endswith(".cs") and "/obj/" not in normalized:
                changed_api_files.append(normalized.removeprefix("backend/"))

        changed_api_files = sorted(set(changed_api_files))
        for changed_file in changed_api_files:
            row = next((item for item in rows if item["filename"] == changed_file), None)
            if row is None or row["line_valid"] == 0:
                warnings.append(f"Changed backend file has no coverable lines in report: {changed_file}")
                continue

            changed_file_results.append(row)
            if row["line_pct"] < args.changed_line_target:
                violations.append(
                    f"Backend changed file below line target: {changed_file} -> {row['line_pct']:.2f}% < {args.changed_line_target:.2f}%"
                )
            if row["branch_valid"] > 0 and row["branch_pct"] < args.changed_branch_target:
                violations.append(
                    f"Backend changed file below branch target: {changed_file} -> {row['branch_pct']:.2f}% < {args.changed_branch_target:.2f}%"
                )

    lowest_files = sorted(rows, key=lambda item: item["line_pct"])[:8]
    summary_lines = [
        "### Backend Coverage",
        f"- Report: `{report_path}`",
        f"- API lines: {total_line_covered}/{total_line_valid} ({overall_line_pct:.2f}%)",
        f"- API branches: {total_branch_covered}/{total_branch_valid} ({overall_branch_pct:.2f}%)" if total_branch_valid else "- API branches: no branch data",
    ]
    if lowest_files:
        summary_lines.append("- Lowest-covered API files:")
        for item in lowest_files:
            branch_text = f", branches {item['branch_pct']:.2f}%" if item["branch_valid"] else ""
            summary_lines.append(
                f"  - {item['filename']}: lines {item['line_pct']:.2f}%{branch_text}"
            )
    if changed_file_results:
        summary_lines.append("- Changed API files:")
        for item in changed_file_results:
            summary_lines.append(
                f"  - {item['filename']}: lines {item['line_pct']:.2f}%, branches {item['branch_pct']:.2f}%"
                if item["branch_valid"]
                else f"  - {item['filename']}: lines {item['line_pct']:.2f}%"
            )
    if warnings:
        summary_lines.append("- Warnings:")
        for warning in warnings:
            summary_lines.append(f"  - {warning}")

    markdown = "\n".join(summary_lines) + "\n"
    print(markdown)
    append_step_summary(markdown)

    if violations:
        for violation in violations:
            print(violation, file=sys.stderr)
        return 1

    return 0


def load_json(path: str) -> dict:
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def normalize_frontend_summary_key(key: str) -> str:
    normalized = normalize_path(key)
    if "/frontend/" in normalized:
        normalized = normalized.split("/frontend/", 1)[1]
    if normalized.startswith("frontend/"):
        normalized = normalized.removeprefix("frontend/")
    return normalized


def metric_pct(metric: dict) -> float:
    return float(metric.get("pct", 0.0))


def frontend_command(args: argparse.Namespace) -> int:
    thresholds = load_json(args.threshold_file)
    summary = load_json(args.summary_file)
    total = summary["total"]
    warnings: list[str] = []
    violations: list[str] = []

    for metric, minimum in thresholds["minimumFloor"].items():
        if thresholds["global"][metric] < minimum:
            violations.append(
                f"Frontend configured global threshold for {metric} is below the minimum floor: {thresholds['global'][metric]} < {minimum}"
            )

    for metric, target in thresholds["changedFiles"].items():
        if target < 80:
            violations.append(f"Frontend changed-file threshold for {metric} must remain at least 80, found {target}")

    if args.event_name == "pull_request" and args.base_ref:
        subprocess.run(
            ["git", "fetch", "--no-tags", "origin", args.base_ref],
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            text=True,
        )
        try:
            previous_text = run_git(["show", f"origin/{args.base_ref}:frontend/coverage-thresholds.json"])
            previous_thresholds = json.loads(previous_text)
            for section in ("global", "minimumFloor", "changedFiles"):
                for metric, value in thresholds[section].items():
                    previous_value = previous_thresholds.get(section, {}).get(metric)
                    if previous_value is not None and value < previous_value:
                        violations.append(
                            f"Frontend threshold ratchet violation: {section}.{metric} decreased from {previous_value} to {value}"
                        )
        except subprocess.CalledProcessError:
            warnings.append("Unable to compare frontend thresholds against the base branch; skipping ratchet check.")

    overall_metrics = {
        "lines": metric_pct(total["lines"]),
        "statements": metric_pct(total["statements"]),
        "functions": metric_pct(total["functions"]),
        "branches": metric_pct(total["branches"]),
    }
    for metric, required in thresholds["global"].items():
        if overall_metrics[metric] < required:
            violations.append(
                f"Frontend global {metric} coverage below threshold: {overall_metrics[metric]:.2f}% < {required:.2f}%"
            )

    by_file = {
        normalize_frontend_summary_key(key): value
        for key, value in summary.items()
        if key != "total"
    }

    changed_file_results = []
    if args.event_name == "pull_request":
        changed_paths = get_changed_paths(args.base_ref, args.event_name)
        changed_frontend_files = []
        for path in changed_paths:
            normalized = normalize_path(path)
            if not normalized.startswith("frontend/src/"):
                continue
            if not normalized.endswith((".ts", ".tsx")):
                continue
            if normalized.endswith((".test.ts", ".test.tsx")) or "/test/" in normalized:
                continue
            changed_frontend_files.append(normalized.removeprefix("frontend/"))

        changed_frontend_files = sorted(set(changed_frontend_files))
        for changed_file in changed_frontend_files:
            file_summary = by_file.get(changed_file)
            if file_summary is None:
                warnings.append(f"Changed frontend file is missing from coverage summary: {changed_file}")
                continue

            lines_total = int(file_summary["lines"].get("total", 0))
            if lines_total == 0:
                warnings.append(f"Changed frontend file has no coverable lines: {changed_file}")
                continue

            file_metrics = {
                "lines": metric_pct(file_summary["lines"]),
                "statements": metric_pct(file_summary["statements"]),
                "functions": metric_pct(file_summary["functions"]),
                "branches": metric_pct(file_summary["branches"]),
            }
            changed_file_results.append((changed_file, file_metrics))
            for metric, required in thresholds["changedFiles"].items():
                if file_metrics[metric] < required:
                    violations.append(
                        f"Frontend changed file below {metric} target: {changed_file} -> {file_metrics[metric]:.2f}% < {required:.2f}%"
                    )

    lowest_files = []
    for key, value in by_file.items():
        lines_total = int(value["lines"].get("total", 0))
        if lines_total == 0:
            continue
        lowest_files.append(
            (
                key,
                metric_pct(value["lines"]),
                metric_pct(value["statements"]),
                metric_pct(value["functions"]),
                metric_pct(value["branches"]),
            )
        )
    lowest_files.sort(key=lambda item: (item[1], item[2], item[3], item[4]))

    summary_lines = [
        "### Frontend Coverage",
        f"- Lines: {overall_metrics['lines']:.2f}%",
        f"- Statements: {overall_metrics['statements']:.2f}%",
        f"- Functions: {overall_metrics['functions']:.2f}%",
        f"- Branches: {overall_metrics['branches']:.2f}%",
        f"- Global thresholds: {json.dumps(thresholds['global'], sort_keys=True)}",
        f"- Changed-file thresholds: {json.dumps(thresholds['changedFiles'], sort_keys=True)}",
    ]
    if lowest_files:
        summary_lines.append("- Lowest-covered frontend files:")
        for key, lines_pct, statements_pct, functions_pct, branches_pct in lowest_files[:8]:
            summary_lines.append(
                f"  - {key}: lines {lines_pct:.2f}%, statements {statements_pct:.2f}%, functions {functions_pct:.2f}%, branches {branches_pct:.2f}%"
            )
    if changed_file_results:
        summary_lines.append("- Changed frontend files:")
        for key, metrics in changed_file_results:
            summary_lines.append(
                f"  - {key}: lines {metrics['lines']:.2f}%, statements {metrics['statements']:.2f}%, functions {metrics['functions']:.2f}%, branches {metrics['branches']:.2f}%"
            )
    if warnings:
        summary_lines.append("- Warnings:")
        for warning in warnings:
            summary_lines.append(f"  - {warning}")

    markdown = "\n".join(summary_lines) + "\n"
    print(markdown)
    append_step_summary(markdown)

    if violations:
        for violation in violations:
            print(violation, file=sys.stderr)
        return 1

    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Coverage gates for CI")
    subparsers = parser.add_subparsers(dest="command", required=True)

    backend = subparsers.add_parser("backend")
    backend.add_argument("--report")
    backend.add_argument("--event-name", default="")
    backend.add_argument("--base-ref", default="")
    backend.add_argument("--line-min", type=float, required=True)
    backend.add_argument("--line-target", type=float, required=True)
    backend.add_argument("--branch-min", type=float, required=True)
    backend.add_argument("--branch-target", type=float, required=True)
    backend.add_argument("--changed-line-target", type=float, required=True)
    backend.add_argument("--changed-branch-target", type=float, required=True)
    backend.set_defaults(func=backend_command)

    frontend = subparsers.add_parser("frontend")
    frontend.add_argument("--summary-file", required=True)
    frontend.add_argument("--threshold-file", required=True)
    frontend.add_argument("--event-name", default="")
    frontend.add_argument("--base-ref", default="")
    frontend.set_defaults(func=frontend_command)

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())