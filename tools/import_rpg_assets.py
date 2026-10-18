"""Import a game-ready Library32 scene and its generated sprite pack.

Source files stay untouched. Run from any directory; override --media if needed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
CONTENT = ROOT / 'BeatEngine/BeatEngine.Core/Content'


def import_assets(media, scene_path=Path('Maps/Library32/scene.json')):
    scene_file = media / scene_path
    scene = json.loads(scene_file.read_text(encoding='utf-8'))
    atlas = json.loads((media / scene['atlas']).read_text(encoding='utf-8'))
    sources = {
        'Rpg/Maps/test.map': media / scene['map'],
        'Rpg/scene.json': scene_file,
        'Rpg/Textures/ground.png': media / scene['ground'],
        'Rpg/Textures/player.png': media / scene['playerWalk'],
        'Rpg/Textures/player_idle.png': media / scene['playerIdle'],
        **{f'Rpg/Textures/{name}.png': media / entry['file'] for name, entry in sorted(atlas['assets'].items())}
    }
    for source in sources.values():
        if not source.is_file():
            raise FileNotFoundError(source)
    provenance = {}
    for target, source in sources.items():
        output = CONTENT / target
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, output)
        provenance[target] = {'source': source.relative_to(media).as_posix(),
                              'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}
    (CONTENT / 'Rpg/source-assets.json').write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')

    manifest = '''# RPG Prototype: assets imported from C:/CommonMedia.
/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/config:$(Configuration)
/profile:Reach
/compress:False

#begin Rpg/Maps/test.map
/copy:Rpg/Maps/test.map

#begin Rpg/scene.json
/copy:Rpg/scene.json

#begin Fonts/RpgHud.spritefont
/importer:FontDescriptionImporter
/processor:FontDescriptionProcessor
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:Fonts/RpgHud.spritefont
'''
    for name in sources:
        if not name.endswith('.png'):
            continue
        manifest += f'''
#begin {name}
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyEnabled=False
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=True
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:{name}
'''
    (CONTENT / 'BeatEngine.mgcb').write_text(manifest, encoding='utf-8')
    print(f'Imported {len(sources)} Library32 scene/assets. Updated content manifest.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--media', type=Path, default=Path('C:/CommonMedia'))
    parser.add_argument('--scene', type=Path, default=Path('Maps/Library32/scene.json'))
    args = parser.parse_args()
    import_assets(args.media.resolve(), args.scene)
