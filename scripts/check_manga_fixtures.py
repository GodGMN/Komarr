#!/usr/bin/env python3
"""Validate the reviewed manga release corpus and report parser errors separately."""

import argparse
import json
from collections import Counter
from decimal import Decimal
from pathlib import Path

CORPUS = Path(__file__).resolve().parents[1] / "tests/Parser/Fixtures/nyaa-public-2026-09-27.jsonl"
UNITS = {"Volume", "Chapter", "Unknown"}


def load_jsonl(path):
    with path.open(encoding="utf-8") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def validate(fixtures):
    assert len(fixtures) >= 500, "At least 500 reviewed release names are required before automatic grabs"
    assert len({row["input"] for row in fixtures}) == len(fixtures), "Release names must be unique"
    assert len({row["sourceQuery"] for row in fixtures}) >= 10, "Corpus needs varied source queries"
    assert all(row.get("reviewed") is True for row in fixtures), "Every fixture needs review"

    counts = Counter()
    for row in fixtures:
        assert row["input"].strip() and row["source"] == "Nyaa"
        expected = row["expected"]
        unit = expected["unitType"]
        assert unit in UNITS, row["input"]
        assert isinstance(expected["isPack"], bool), row["input"]
        if unit == "Unknown":
            assert expected["confidence"] == "Low", row["input"]
            assert expected["start"] is None and expected["end"] is None, row["input"]
        else:
            assert expected["title"].strip(), row["input"]
            assert expected["confidence"] in {"High", "Medium"}, row["input"]
            assert Decimal(expected["start"]) <= Decimal(expected["end"]), row["input"]
        counts[unit] += 1

    assert counts["Volume"] >= 200 and counts["Chapter"] >= 100 and counts["Unknown"] >= 100, counts
    assert sum("unsafeReason" in row["expected"] for row in fixtures) >= 50
    return counts


def evaluate(fixtures, predictions):
    """A wrong positive counts as both a false positive and a missed expected release."""

    def same_number(left, right):
        if left is None or right is None:
            return left == right
        try:
            return Decimal(str(left)) == Decimal(str(right))
        except Exception:
            return str(left).casefold() == str(right).casefold()

    counts = Counter()
    for row in fixtures:
        expected = row["expected"]
        actual = predictions.get(row["input"], {"unitType": "Unknown"})
        expected_positive = expected["unitType"] != "Unknown"
        actual_positive = actual["unitType"] != "Unknown"
        exact = (
            expected_positive
            and actual_positive
            and expected["unitType"] == actual["unitType"]
            and same_number(expected["start"], actual.get("start"))
            and same_number(expected["end"], actual.get("end"))
            and expected["title"].casefold() == (actual.get("title") or "").casefold()
        )
        if exact:
            counts["truePositives"] += 1
        elif expected_positive and actual_positive:
            counts["falsePositives"] += 1
            counts["falseNegatives"] += 1
        elif expected_positive:
            counts["falseNegatives"] += 1
        elif actual_positive:
            counts["falsePositives"] += 1
        else:
            counts["trueNegatives"] += 1

    return {key: counts[key] for key in ("truePositives", "trueNegatives", "falsePositives", "falseNegatives")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--predictions", type=Path, help="JSONL rows with input and parser result")
    args = parser.parse_args()
    fixtures = load_jsonl(CORPUS)
    categories = validate(fixtures)
    predictions = {}
    if args.predictions:
        predictions = {row["input"]: row["actual"] for row in load_jsonl(args.predictions)}
        assert len(predictions) == len(fixtures), "Parser predictions must cover the full fixture corpus"
    report = {
        "fixtures": len(fixtures),
        "categories": dict(categories),
        "mode": "parser" if args.predictions else "unresolved baseline",
        **evaluate(fixtures, predictions),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
