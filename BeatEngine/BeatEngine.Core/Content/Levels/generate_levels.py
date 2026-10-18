#!/usr/bin/env python3
"""Curate numbered Spanish levels. Use --write to correct or --check to validate.

curated_words.json is the editorial authority for spelling and syllabification.
Structural checks alone cannot establish that a word is Spanish.
"""
import argparse
import json
from itertools import combinations
from pathlib import Path
import re

LEVEL_DIRECTORY = Path(__file__).resolve().parent
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


def parse_level(path):
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    if not lines:
        raise ValueError(f'{path.name}: empty level')
    rows = {1: {}, 2: {}, 3: {}}
    for line in lines[1:]:
        if not line.strip():
            continue
        match = re.fullmatch(r'([A-ZÁÉÍÓÚÜÑ]+)\(([1-3]),([1-4])\)', line.strip())
        if not match:
            raise ValueError(f'{path.name}: malformed tile {line!r}')
        syllable, row, slot = match.groups()
        row, slot = int(row), int(slot)
        if slot in rows[row]:
            raise ValueError(f'{path.name}: duplicate slot')
        rows[row][slot] = syllable
    words = []
    for slots in rows.values():
        if sorted(slots) != list(range(1, len(slots) + 1)):
            raise ValueError(f'{path.name}: nonconsecutive slots')
        words.append([slots[slot] for slot in sorted(slots)])
    return lines[0].strip(), words


def generate_level_file(file_number, theme, words):
    if not validate_level(words):
        raise ValueError(f'Invalid words for level {file_number}')
    lines = [theme]
    for row, syllables in enumerate(words, 1):
        lines.extend(f'{s}({row},{slot})' for slot, s in enumerate(syllables, 1))
        lines.append('')
    return '\n'.join(lines).rstrip() + '\n'


def combo_key(words):
    return tuple(sorted(''.join(word) for word in words))


def curated(theme, words):
    return all(word in THEMES_AND_WORDS.get(theme, []) for word in words)


def generate_all_levels(write=False):
    files = sorted(LEVEL_DIRECTORY.glob('[0-9]*.txt'), key=lambda p: int(p.stem))
    available = {theme: find_valid_combinations(words) for theme, words in THEMES_AND_WORDS.items()}
    used, pending, planned = set(), [], []
    # Reserve valid existing levels before allocating replacements.
    for path in files:
        try:
            theme, words = parse_level(path)
        except ValueError:
            theme = path.read_text(encoding='utf-8-sig').splitlines()[0].strip()
            words = []
        key = (theme, combo_key(words))
        if validate_level(words) and curated(theme, words) and key not in used:
            used.add(key)
        else:
            pending.append((path, theme, words))
    for path, theme, old_words in pending:
        candidates = [combo for combo in available.get(theme, []) if (theme, combo_key(combo)) not in used]
        if not candidates:
            raise ValueError(f'Insufficient curated combinations for {theme}; no files written')
        old_names = {normalize(''.join(word)) for word in old_words}
        chosen = min(candidates, key=lambda combo: (
            -sum(normalize(''.join(word)) in old_names for word in combo), combo_key(combo)))
        used.add((theme, combo_key(chosen)))
        planned.append((path, generate_level_file(path.stem, theme, chosen)))
    if write:
        for path, content in planned:
            path.write_text(content, encoding='utf-8')
    print(f'{len(files)} levels; {len(planned)} corrections' + (' applied.' if write else ' required.'))
    return len(planned)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--write', action='store_true')
    group.add_argument('--check', action='store_true')
    args = parser.parse_args()
    corrections = generate_all_levels(write=args.write)
    if args.check and corrections:
        raise SystemExit(1)
