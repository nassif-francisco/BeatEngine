# RPG Prototype mobile port

This project now runs the exploration prototype from `C:/RPGPrototype` on
MonoGame. Android launches directly into the RPG in landscape. The shared
launcher class is still named `BeatEngineGame` so the platform projects keep their
references. The previous EnSilabas source/assets remain in the repository, but
are excluded from the active Core compile and content build.

## Library first scene

The active scene uses the new pixel-art pack at `C:/CommonMedia/Environment/Packs/Library32` and hero at `C:/CommonMedia/Player/Library32`. There are 56 modular tiles, 32 props and 32 character animation frames. Original desktop assets remain intact in CommonMedia.

The character uses 32px frames, four rows (Down, Left, Right, Up), four columns, and separate walking/idle sheets. Movement is 96 world pixels per second. The authored library has walkable corridors, bookshelf collisions and depth-sorted furniture. Scene configuration is imported from `C:/CommonMedia/Maps/Library32/scene.json`. Save version 2 resets old map coordinates safely.

## Controls and persistence

- Phone: drag the lower-left joystick. Tap **PAUSA** to resume or return to the
  map's initial position.
- Pause menu: choose the left or right joystick position; the selection is saved independently of map progress.
- Desktop: WASD/arrows to move; P/Escape to pause.
- Controller: left stick to move; Start/Back to pause.
- The camera adapts to the screen aspect ratio without stretching pixel art.
- Backgrounding the app clears touch input, pauses, and saves. Player position
  and facing are also saved every three seconds after movement and when exiting.
- Saves live in the app's local data directory under `RPGPrototype/save.json`.
  Writes use a flushed temporary file followed by atomic replacement. Corrupt,
  unsupported, or obstructed saved positions fall back to the initial position.

## Build and run

From `C:/BeatEngine`:

```powershell
dotnet run --project BeatEngine/BeatEngine.DesktopGL/BeatEngine.DesktopGL.csproj
dotnet build BeatEngine/BeatEngine.Android/BeatEngine.Android.csproj -c Release
dotnet run --project tools/tests/Rpg/Rpg.Tests.csproj
python tools/verify_rpg_apk.py
```

Android APK:
`BeatEngine/BeatEngine.Android/bin/Release/net9.0-android/com.rpgprototype.app-Signed.apk`.
The app label is **RPG Prototype**, assembly is `RPGPrototype`, and package ID is
`com.rpgprototype.app`. This installs separately from EnSilabas and has separate
save data. Android is the verified mobile target; iOS requires building and
running with an Apple toolchain.

## Map and assets

Imported assets are self-contained under `BeatEngine.Core/Content/Rpg`.
`source-assets.json` records the original relative filenames and SHA-256 hashes.
The app does not need `C:/CommonMedia` at runtime. To reimport authored changes:

```powershell
python tools/import_rpg_assets.py --media C:/CommonMedia
```

The importer copies only referenced assets and rebuilds the RPG content manifest;
it never changes the original desktop files. Do not run the old
`generate_art.py` for this game: it belongs to EnSilabas.

Port reference: `C:/RPGPrototype` commit
`a7946d47d64c68b9c4dd066076a4300104507d22`. This repository's earlier Android
launcher, shared-Core and scene/input work was reviewed before adapting it.
