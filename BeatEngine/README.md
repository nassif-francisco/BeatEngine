# En Silabas

A MonoGame syllable puzzle. Drag the syllables into three word rows, using the clue to find the words and reading each row from left to right.

## Visual direction

The title is four colored syllable tiles: EN / SI / LA / BAS, shared by the menu and compact in-game header. A warm paper background, charcoal typography, softly squared tiles, and continuous sage word trays give En Silabas its own visual identity. Yellow, mint, ivory, and gray tile faces are decorative. Each tray represents a whole word; subtle baseline guides suggest tile positions without dividing the row into boxes. Clues occupy a bounded band above the board heading. Controls, tutorials, feedback, and platform art share the same geometry. No wood texture or raised tile effects are used.

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

Reference layouts (composed from the assets, not runtime screenshots) are in `ArtPreview/`. The script uses the bundled Avenir and Roboto fonts and no external artwork. Existing content filenames remain compatible with the MonoGame pipeline.

## Project layout

- `BeatEngine.Core`: game, scenes, puzzle logic, and shared content.
- `BeatEngine.DesktopGL`: desktop launcher and content build.
- `BeatEngine.WindowsDX`, `BeatEngine.Android`, `BeatEngine.iOS`: platform launchers.
- `Documentation`: inherited platformer starter-kit documentation, retained for reference.

Internal project names and namespaces retain BeatEngine to preserve references; the displayed game name is En Silabas.
