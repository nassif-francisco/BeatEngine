"""Regression checks for the complete shipped Spanish level collection."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
DIRECTORY = ROOT / "BeatEngine/BeatEngine.Core/Content/Levels"
spec = importlib.util.spec_from_file_location("generate_levels", DIRECTORY / "generate_levels.py")
levels = importlib.util.module_from_spec(spec)
spec.loader.exec_module(levels)


class LevelTests(unittest.TestCase):
    def test_content_pipeline_packages_only_the_catalog(self):
        manifest = (DIRECTORY.parent / "BeatEngine.mgcb").read_text(encoding="utf-8-sig")
        copies = [line for line in manifest.splitlines() if line.startswith("/copy:Levels/")]
        self.assertEqual(copies, ["/copy:Levels/levels.json"])

    def test_all_shipped_levels(self):
        records = levels.load_catalog()
        self.assertEqual([record['id'] for record in records], list(range(1, 1010)))
        self.assertEqual(list(DIRECTORY.glob('[0-9]*.txt')), [])
        seen = set()
        for record in records:
            with self.subTest(level=record['id']):
                theme, words = levels.parse_level(record)
                self.assertTrue(levels.validate_level(words))
                self.assertTrue(levels.curated(theme, words))
                key = (theme, levels.combo_key(words))
                self.assertNotIn(key, seen)
                seen.add(key)

    def test_corrected_spanish_spellings_and_splits(self):
        words = {"".join(word): word for group in levels.THEMES_AND_WORDS.values() for word in group}
        for word, syllables in {
            "MANZANA": ["MAN", "ZA", "NA"],
            "ÁGUILA": ["Á", "GUI", "LA"],
            "GUAYABA": ["GUA", "YA", "BA"],
            "SANDÍA": ["SAN", "DÍ", "A"],
            "LÁCTEOS": ["LÁC", "TE", "OS"],
            "CUADERNO": ["CUA", "DER", "NO"],
            "GUITARRA": ["GUI", "TA", "RRA"],
            "BOMBERO": ["BOM", "BE", "RO"],
            "CEREBRO": ["CE", "RE", "BRO"],
            "CAMIÓN": ["CA", "MIÓN"],
            "TRANVÍA": ["TRAN", "VÍ", "A"],
        }.items():
            with self.subTest(word=word):
                self.assertEqual(words[word], syllables)

    def test_rejects_monosyllable(self):
        self.assertFalse(levels.validate_level([["SOL"], ["CO", "NE", "JO"], ["JI", "RA", "FA"]]))

    def test_rejects_two_disyllables(self):
        self.assertFalse(levels.validate_level([["GA", "TO"], ["LU", "NA"], ["JI", "RA", "FA"]]))

    def test_requires_two_exactly_three_syllable_words(self):
        self.assertFalse(levels.validate_level([["E", "LE", "FAN", "TE"], ["CO", "NE", "JO"], ["A", "BO", "GA", "DO"]]))

    def test_rejects_repeated_syllable_inside_word(self):
        self.assertFalse(levels.validate_level([["PA", "PA", "YA"], ["CO", "NE", "JO"], ["JI", "RA", "FA"]]))

    def test_rejects_repeated_syllable_across_words_even_with_accent(self):
        self.assertFalse(levels.validate_level([["PÁ", "JA", "RO"], ["PA", "LO", "MA"], ["CO", "NE", "JO"]]))

    def test_accepts_three_trisyllables_or_one_four_syllable_word(self):
        self.assertTrue(levels.validate_level([["CA", "BA", "LLO"], ["JI", "RA", "FA"], ["CO", "NE", "JO"]]))
        self.assertTrue(levels.validate_level([["CA", "BA", "LLO"], ["JI", "RA", "FA"], ["E", "LE", "FAN", "TE"]]))

    def test_rejects_even_one_disyllable(self):
        self.assertFalse(levels.validate_level([["CA", "BA", "LLO"], ["JI", "RA", "FA"], ["O", "SO"]]))


if __name__ == "__main__":
    unittest.main()
