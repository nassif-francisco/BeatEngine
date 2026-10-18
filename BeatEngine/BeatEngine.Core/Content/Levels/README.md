# Spanish level curation

`levels.json` contains all 1,009 playable levels in one UTF-8 JSON array. Each
entry has an `id`, a `theme`, and `words`: three arrays of Spanish syllables.
Array order determines word positions and syllable order. IDs preserve saved
progress. The game caches the catalog and selects levels by ID. The content
pipeline copies this single file into the game's Content/Levels directory.

```json
{"id": 5, "theme": "ANIMALES", "words": [["CA", "BA", "LLO"], ["JI", "RA", "FA"], ["A", "VES", "TRUZ"]]}
```

`curated_words.json` contains reviewed Spanish words as explicit syllable arrays,
including accents. It is the source for `generate_levels.py`; do not replace it
with arbitrary letter chunks or an automatic hyphenation dictionary. Consult the
[RAE rules on vowel sequences](https://www.rae.es/ortografía-básica/uso-de-la-tilde/las-reglas-de-acentuación-gráfica/la-acentuación-gráfica-de-las-palabras-con-secuencias-vocálicas)
when checking diphthongs and hiatuses.

Each level in the catalog must have:

- Exactly three words from its theme's reviewed vocabulary.
- At least two words with exactly three syllables.
- At least three syllables in every word; no one- or two-syllable words.
- No repeated syllable within or across words, ignoring accents for this check.
- No word longer than four tiles, to fit the existing trays.

Words may recur in different levels. Complete word combinations are unique within
each theme. The generator preserves compliant levels, themes, and numbering,
then replaces invalid combinations deterministically, retaining familiar words
where possible. It plans all replacements before replacing the catalog file.

From the repository root:

```powershell
python BeatEngine/BeatEngine.Core/Content/Levels/generate_levels.py --check
python BeatEngine/BeatEngine.Core/Content/Levels/generate_levels.py --write
python -m unittest discover -s tools/tests -p test_levels.py
dotnet run --project tools/tests/LevelCatalog/LevelCatalog.Tests.csproj
```

The tests check all 1,009 shipped levels and regression cases. Structural checks
cannot verify Spanish vocabulary or pronunciation; new vocabulary still needs
editorial review.
