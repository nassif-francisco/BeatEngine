"""Import a game-ready Library32 scene and its generated sprite pack.

Source files stay untouched. Run from any directory; override --media if needed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import os
import math

ROOT = Path(__file__).resolve().parents[1]
CONTENT = ROOT / 'BeatEngine/BeatEngine.Core/Content'


def project_settings(media):
    project = media / 'rainbow.project'
    if not project.exists():
        return {}
    return dict(line.split('=', 1) for line in project.read_text(encoding='utf-8-sig').splitlines()
                if line and not line.startswith('#') and '=' in line)


def validate_map(path, assets, scene=None):
    section = None
    counts = {'BACKGROUND': 0, 'FOREGROUND': 0, 'HITBOXES': 0}
    obstacles = []
    for number, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
        if not line.strip():
            continue
        if line in counts:
            section = line
            continue
        try:
            if section in ('BACKGROUND', 'FOREGROUND'):
                name, x, y = line.split(',')
                if name not in assets:
                    raise ValueError(f'unknown sprite {name!r}; add it to atlas.json before importing')
                if not all(math.isfinite(float(value)) for value in (x, y)):
                    raise ValueError('non-finite coordinate')
            elif section == 'HITBOXES':
                name, coordinates = line.split(',', 1)
                points = [tuple(map(float, point.split(','))) for point in coordinates.split(';')]
                if len(points) != 4 or any(len(point) != 2 or not all(map(math.isfinite, point)) for point in points):
                    raise ValueError('hitbox requires four finite points')
                xs, ys = zip(*points)
                if max(xs) <= min(xs) or max(ys) <= min(ys):
                    raise ValueError('hitbox has no area')
                obstacles.append((min(xs), min(ys), max(xs), max(ys)))
            else:
                raise ValueError('missing layer header')
            counts[section] += 1
        except ValueError as error:
            raise ValueError(f'{path}:{number}: {error}') from error
    if not counts['BACKGROUND']:
        raise ValueError('Map must contain background tiles')
    if scene:
        x, y = scene['spawn']
        bx, by, width, height = scene['bounds']
        left, top, right, bottom = x + 10, y + 23, x + 22, y + 31
        if not (left >= bx and top >= by and right <= bx + width and bottom <= by + height):
            raise ValueError('Player spawn is outside scene bounds; adjust scene.json')
        if any(left < r and right > l and top < b and bottom > t for l, t, r, b in obstacles):
            raise ValueError('Player spawn is blocked by a hitbox; move the object or adjust scene.json')
    return counts


def write_if_changed(path, data):
    if not path.exists() or path.read_bytes() != data:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def import_assets(media, scene_path=None):
    project = project_settings(media)
    scene_path = scene_path or Path(project.get('scene', 'Maps/Library32/scene.json'))
    scene_file = media / scene_path
    scene = json.loads(scene_file.read_text(encoding='utf-8'))
    atlas = json.loads((media / scene['atlas']).read_text(encoding='utf-8'))
    if project.get('map', scene['map']) != scene['map']:
        raise ValueError('rainbow.project map must match scene.json map')
    validate_map(media / scene['map'], atlas['assets'], scene)
    collisions = ''.join(name + ',' + ','.join(map(str, entry['collision'])) + '\n'
                         for name, entry in atlas['assets'].items() if entry.get('collision'))
    write_if_changed(media / 'Maps/Library32/rainbow-collisions.csv', collisions.encode('utf-8'))
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
        write_if_changed(output, source.read_bytes())
        provenance[target] = {'source': source.relative_to(media).as_posix(),
                              'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}
    write_if_changed(CONTENT / 'Rpg/source-assets.json', (json.dumps(provenance, indent=2) + '\n').encode('utf-8'))

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
    write_if_changed(CONTENT / 'BeatEngine.mgcb', manifest.encode('utf-8'))
    print(f'Imported {len(sources)} Library32 scene/assets. Updated content manifest.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--media', type=Path, default=Path(os.environ.get('COMMON_MEDIA_ROOT', 'C:/CommonMedia')))
    parser.add_argument('--scene', type=Path)
    args = parser.parse_args()
    import_assets(args.media.resolve(), args.scene)
