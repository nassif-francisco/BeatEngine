# En Silabas

A MonoGame syllable puzzle. Drag the syllables into three word rows, using the clue to find the words and reading each row from left to right.

## Visual direction

Black ink on white, square outlined cells, monospaced type, and generous space inspired by Sudoku. All original source images have been replaced, including the menu, tutorial, tiles, word rows, feedback, legacy variants, and platform icons/splashes. Press feedback uses gray; completion uses a simple check mark. Audio is retained.

The clue starts hidden. "Mostrar Pista" reveals it and deducts 5 points once per level. The deduction is saved immediately. Completion feedback appears below the word rows.

The design resolution is 1200 × 2664. Tiles are 220 × 220 inside 225-pixel word-row slots. Tile labels are measured and centered at runtime. The HUD font includes Latin-1 characters for Spanish clues.

## Run

From the repository root:

```powershell
dotnet run --project BeatEngine/BeatEngine.DesktopGL/BeatEngine.DesktopGL.csproj
```

Mouse input is enabled for desktop play. Touch input uses the same puzzle coordinates.

## Artwork

Regenerate the geometric assets and reference layouts using Python and Pillow:

```powershell
python tools/generate_art.py
```

Reference layouts (composed from the assets, not runtime screenshots) are in `ArtPreview/`. The script uses the bundled Consolas font and no external artwork. Existing content filenames remain compatible with the MonoGame pipeline.

## Project layout

- `BeatEngine.Core`: game, scenes, puzzle logic, and shared content.
- `BeatEngine.DesktopGL`: desktop launcher and content build.
- `BeatEngine.WindowsDX`, `BeatEngine.Android`, `BeatEngine.iOS`: platform launchers.
- `Documentation`: inherited platformer starter-kit documentation, retained for reference.

Internal project names and namespaces retain BeatEngine to preserve references; the displayed game name is En Silabas.
