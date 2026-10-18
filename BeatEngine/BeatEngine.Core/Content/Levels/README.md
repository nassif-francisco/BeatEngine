# Spanish level curation

`curated_words.json` contains reviewed Spanish words as explicit syllable arrays,
including accents. It is the source for `generate_levels.py`; do not replace it
with arbitrary letter chunks or an automatic hyphenation dictionary. Consult the
[RAE rules on vowel sequences](https://www.rae.es/ortografía-básica/uso-de-la-tilde/las-reglas-de-acentuación-gráfica/la-acentuación-gráfica-de-las-palabras-con-secuencias-vocálicas)
when checking diphthongs and hiatuses.

Each numbered level must have:

- Exactly three words from its theme's reviewed vocabulary.
- At least two words with exactly three syllables.
- At least three syllables in every word; no one- or two-syllable words.
- No repeated syllable within or across words, ignoring accents for this check.
- No word longer than four tiles, to fit the existing trays.

Words may recur in different levels. Complete word combinations are unique within
each theme. The generator preserves compliant levels, themes, and numbering,
then replaces invalid combinations deterministically, retaining familiar words
where possible. It plans all replacements before writing any files.

From the repository root:

```powershell
python BeatEngine/BeatEngine.Core/Content/Levels/generate_levels.py --check
python BeatEngine/BeatEngine.Core/Content/Levels/generate_levels.py --write
python -m unittest discover -s tools/tests -p test_levels.py
```

The tests check all 1,009 shipped levels and regression cases. Structural checks
cannot verify Spanish vocabulary or pronunciation; new vocabulary still needs
editorial review.
