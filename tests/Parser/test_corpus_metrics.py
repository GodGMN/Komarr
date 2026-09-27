import unittest

from scripts.check_manga_fixtures import evaluate


class CorpusMetricsTest(unittest.TestCase):
    def test_numeric_padding_does_not_create_a_false_negative(self):
        fixtures = [{"input": "Manga v01", "expected": {"title": "Manga", "unitType": "Volume", "start": "01", "end": "01"}}]
        predictions = {"Manga v01": {"title": "Manga", "unitType": "Volume", "start": "1", "end": "1"}}

        report = evaluate(fixtures, predictions)

        self.assertEqual(report["truePositives"], 1)
        self.assertEqual(report["falseNegatives"], 0)

    def test_wrong_positive_is_visible_as_false_positive_and_false_negative(self):
        fixtures = [
            {"input": "Manga v01", "expected": {"title": "Manga", "unitType": "Volume", "start": "01", "end": "01"}},
            {"input": "Manga 01", "expected": {"title": None, "unitType": "Unknown", "start": None, "end": None}},
        ]
        predictions = {
            "Manga v01": {"title": "Manga", "unitType": "Chapter", "start": "01", "end": "01"},
            "Manga 01": {"title": "Manga", "unitType": "Volume", "start": "01", "end": "01"},
        }

        report = evaluate(fixtures, predictions)

        self.assertEqual(report["falsePositives"], 2)
        self.assertEqual(report["falseNegatives"], 1)
        self.assertEqual(report["truePositives"], 0)


if __name__ == "__main__":
    unittest.main()
