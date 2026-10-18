# RPG Prototype mobile port

This project now runs the exploration prototype from `C:/RPGPrototype` on
MonoGame. Android launches directly into the RPG in landscape. The shared
launcher class is still named `BeatEngineGame` so the platform projects keep their
references. The previous EnSilabas source/assets remain in the repository, but
are excluded from the active Core compile and content build.

## Library first scene

The active scene uses the new pixel-art pack at `C:/CommonMedia/Environment/Packs/Library32` and hero at `C:/CommonMedia/Player/Library32`. There are 56 modular tiles, 32 props and 32 character animation frames. Original desktop assets remain intact in CommonMedia.

The character uses 32px frames, four rows (Down, Left, Right, Up), four columns, and separate walking/idle sheets. Dante walks at the configured speed (currently 128 world pixels per second) and runs at 1.5x that speed. The authored library has walkable corridors, bookshelf collisions and depth-sorted furniture. Scene configuration is imported from `C:/CommonMedia/Maps/Library32/scene.json`. Save version 3 stores completed story scenes and migrates version 2 saves.

## Controls and persistence

- Phone: drag the lower-left joystick. Tap **PAUSA** to resume or return to the
  map's initial position.
- The joystick is fixed on the left. Hold **Correr** on the right to run; tap **Hablar** inside an available interaction zone.
- Desktop: WASD/arrows to move; Shift to run; E/Enter to talk/continue; P/Escape to pause.
- Controller: left stick to move; left shoulder to run; A to talk/continue; Start/Back to pause.
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

## Rainbow integration

Rainbow opens the same `C:/CommonMedia/Maps/Library32/library.map`. Save in Rainbow with Ctrl+S, then build the game: `Directory.Build.targets` imports the shared scene before the MonoGame content build. A blocked spawn, invalid hitbox or unknown asset fails the build with a diagnostic. `COMMON_MEDIA_ROOT` / `CommonMediaRoot` can relocate the shared folder. Builds without a shared project use the imported snapshot; `/p:ImportRainbowAssets=false` explicitly skips synchronization. Python 3 is required for synchronization.

See `C:/RainbowProject/README.md` for palette, placement and collision controls. Changes reach installed phones after rebuilding and installing the APK.

## First story scene

Dante meets Abuelo in the central aisle. The actor sprite is `CommonMedia/Environment/Packs/Library32/props/grandfather.png`. Its physical footprint and the larger non-solid `logical:grandfather` zone live in the shared map and can be edited with Rainbow. Rainbow moves/deletes that actor's interaction zone along with its physical hitbox.

The corrected Spanish conversation is authored in `C:/CommonMedia/Dialogs/grandfather_intro.json` and imported automatically. Each press of **Continuar** acknowledges exactly one turn. Movement is suspended during dialogue. Acknowledging the last turn immediately saves checkpoint `library_grandfather_intro`; it survives app restarts and returning to the map entrance. The completed introduction does not replay, and the objective becomes **Busca el libro azul**. Leaving before the last turn keeps the scene unfinished.

Dialogue portraits are configured by speaker name in the shared scene.json `portraits` dictionary, with native 48px PNGs under `C:/CommonMedia/Dialogs/Portraits`. The current speaker's portrait uses nearest-neighbor sampling. Buttons capture touch feedback separately from action availability: pressing Hablar always depresses/highlights it, including when the introduction is complete or Dante is outside the interaction zone. A short pulse makes quick taps visible; holding keeps the button pressed.

Hitboxes whose names start with `logical` are non-blocking game-logic zones; use `logical:grandfather` for Abuelo's conversation. The legacy `interaction:` prefix remains readable for compatibility. Right-click a hitbox in Rainbow to inspect its name and purpose; green outlines identify logical areas, orange outlines physical obstacles. Names survive dragging and resizing.

The right-side action stack includes **Explorar** (press feedback only, reserved for future logic), **Correr**, and **Hablar**. Hablar stays in its normal visual state when entering a conversation zone; it highlights/depresses only in response to user input.

## Reset to a checkpoint

The pause menu's **Set checkpoint to:** field accepts a checkpoint ID, display name, or numeric alias. Tap the field to enter text, then **Aplicar**. Unknown values leave the current save unchanged.

- `inicio`, `Inicio`, `entrada`, or `0`: reset Dante to the entrance and clear story progress, allowing the introduction again.
- `library_grandfather_intro`, `Abuelo`, `Después de hablar con Abuelo`, or `1`: restore the completed introduction and place Dante at the entrance. Future talks show only the blue-book reminder.

Checkpoint definitions are authored in `C:/CommonMedia/Checkpoints/checkpoints.json`; each defines the completed story scenes; every restore uses the scene spawn position. Applying a checkpoint cancels any active dialogue, clears movement input, and immediately saves the restored state. The reminder is authored as `repeatLines` in the conversation JSON and never advances or rewrites the story checkpoint.

Checkpoint IDs and scene descriptions are listed in `BeatEngine.Core/Content/Rpg/Checkpoints/checkpoints.json` (`id` and `description`). They are not listed in the pause menu or keyboard popup. Edit the source at `C:/CommonMedia/Checkpoints/checkpoints.json`; asset import/build copies it into the project.

Every checkpoint restore returns Dante to the initial position defined by `spawn` in the scene JSON, while restoring the selected story progress.
