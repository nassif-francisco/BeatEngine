#!/usr/bin/env python3
"""Curate the consolidated Spanish level catalog. Use --write to correct or --check to validate.

curated_words.json is the editorial authority for spelling and syllabification.
Structural checks alone cannot establish that a word is Spanish.
"""
import argparse
import json
from itertools import combinations
from pathlib import Path
import re

LEVEL_DIRECTORY = Path(__file__).resolve().parent
CATALOG_PATH = LEVEL_DIRECTORY / 'levels.json'
THEMES_AND_WORDS = json.loads((LEVEL_DIRECTORY / 'curated_words.json').read_text(encoding='utf-8'))


def normalize(text):
    # Accents do not make a repeated syllable unique; retain the distinct letter ñ.
    return text.upper().translate(str.maketrans('ÁÉÍÓÚÜ', 'AEIOUU'))


def validate_level(words):
    if len(words) != 3:
        return False
    lengths = [len(word) for word in words]
    if any(length < 3 or length > 4 for length in lengths):
        return False
    if lengths.count(3) < 2:
        return False
    syllables = [normalize(s) for word in words for s in word]
    if any(not re.fullmatch(r'[A-ZÑ]+', s) or not any(v in s for v in 'AEIOU') for s in syllables):
        return False
    return len(syllables) == len(set(syllables))


def find_valid_combinations(words):
    return [combo for combo in combinations(words, 3) if validate_level(combo)]


def load_catalog(path=CATALOG_PATH):
    records = json.loads(path.read_text(encoding='utf-8-sig'))
    if not isinstance(records, list) or not records:
        raise ValueError('The level catalog must be a nonempty array')
    seen = set()
    for record in records:
        if (not isinstance(record, dict) or type(record.get('id')) is not int or
                record['id'] < 1 or record['id'] in seen):
            raise ValueError('Invalid or duplicate level ID')
        seen.add(record['id'])
        parse_level(record)
    return records


def parse_level(record):
    theme, words = record.get('theme'), record.get('words')
    if not isinstance(theme, str) or not theme.strip():
        raise ValueError('Missing level theme')
    if not isinstance(words, list) or any(
            not isinstance(word, list) or any(not isinstance(s, str) for s in word)
            for word in words):
        raise ValueError('Level words must be arrays of syllable strings')
    return theme, words


def combo_key(words):
    return tuple(sorted(''.join(word) for word in words))


def curated(theme, words):
    return all(word in THEMES_AND_WORDS.get(theme, []) for word in words)


def generate_all_levels(write=False):
    records = load_catalog()
    available = {theme: find_valid_combinations(words) for theme, words in THEMES_AND_WORDS.items()}
    used, pending = set(), []
    # Reserve valid existing levels before allocating replacements.
    for record in records:
        theme, words = parse_level(record)
        key = (theme, combo_key(words))
        if validate_level(words) and curated(theme, words) and key not in used:
            used.add(key)
        else:
            pending.append(record)
    for record in pending:
        theme, old_words = parse_level(record)
        candidates = [combo for combo in available.get(theme, []) if (theme, combo_key(combo)) not in used]
        if not candidates:
            raise ValueError(f'Insufficient curated combinations for {theme}; catalog not written')
        old_names = {normalize(''.join(word)) for word in old_words}
        chosen = min(candidates, key=lambda combo: (
            -sum(normalize(''.join(word)) in old_names for word in combo), combo_key(combo)))
        used.add((theme, combo_key(chosen)))
        record['words'] = list(chosen)
    if write and pending:
        temporary = CATALOG_PATH.with_suffix('.json.tmp')
        temporary.write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        temporary.replace(CATALOG_PATH)
    print(f'{len(records)} levels; {len(pending)} corrections' + (' applied.' if write else ' required.'))
    return len(pending)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--write', action='store_true')
    group.add_argument('--check', action='store_true')
    args = parser.parse_args()
    corrections = generate_all_levels(write=args.write)
    if args.check and corrections:
        raise SystemExit(1)
