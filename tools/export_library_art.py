"""Technical export of generated art: slice, nearest-neighbor scale and pack.

Artwork originates in the ImageGen PNGs in Library32/Sources. This script does
not procedurally draw replacement artwork. Originals and the previous art stay
untouched. Output frames have binary alpha and a maximum 64-color palette.
"""
from pathlib import Path
import json
import math
from PIL import Image, ImageDraw, ImageFont

MEDIA = Path('C:/CommonMedia')
PACK = MEDIA / 'Environment/Packs/Library32'
PLAYER = MEDIA / 'Player/Library32'
SOURCES = PACK / 'Sources'

FLOORS = ([f'floor_wood_{i+1:02}' for i in range(8)] +
          [f'floor_stone_{i+1:02}' for i in range(8)] +
          ['carpet_center', 'carpet_n', 'carpet_s', 'carpet_w', 'carpet_e', 'carpet_nw', 'carpet_ne', 'carpet_sw',
           'carpet_se', 'runner_horizontal', 'runner_vertical', 'runner_cross', 'runner_t_n', 'runner_t_s', 'runner_t_w', 'runner_t_e'])
STAIRS = ['stairs_up_top', 'stairs_up_middle', 'stairs_up_bottom', 'stairs_down_top', 'stairs_down_middle', 'stairs_down_bottom', 'doorway_floor', 'landing_wood']
WALLS = ['wall_n_01', 'wall_n_02', 'wall_n_03', 'wall_n_04', 'wall_w', 'wall_e', 'wall_s', 'pillar',
         'wall_corner_nw', 'wall_corner_ne', 'wall_corner_sw', 'wall_corner_se',
         'wall_inner_nw', 'wall_inner_ne', 'wall_inner_sw', 'wall_inner_se']
FURNITURE = [
    ('bookshelf_full', (64, 96)), ('bookshelf_scrolls', (64, 96)),
    ('bookshelf_rare', (64, 96)), ('bookshelf_back', (64, 96)),
    ('bookshelf_low', (64, 64)), ('reading_table', (64, 64)),
    ('reception_desk', (96, 64)), ('double_door', (64, 96)),
    ('armchair', (32, 64)), ('reading_chair', (32, 32)),
    ('ladder', (32, 64)), ('standing_lamp', (32, 64)),
    ('globe', (32, 64)), ('book_cart', (64, 64)),
    ('book_stack', (32, 32)), ('lectern', (32, 64))]
DECOR = [(name, (32, 64) if name == 'arched_window' else (32, 32)) for name in
         ['book_burgundy', 'book_teal', 'book_blue', 'book_ochre', 'open_book', 'scrolls', 'parchment', 'ink_quill',
          'candle', 'wall_sconce', 'hourglass', 'brass_key', 'arched_window', 'wall_clock', 'book_sign', 'potted_fern']]


def cell(image, columns, rows, index):
    x, y = index % columns, index // columns
    return image.crop((round(x * image.width / columns), round(y * image.height / rows),
                       round((x + 1) * image.width / columns), round((y + 1) * image.height / rows)))


def clean(image, opaque=False):
    image = image.convert('RGBA')
    image.putalpha(Image.new('L', image.size, 255) if opaque else image.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    return image.quantize(colors=64, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).convert('RGBA')


def object_cells(image):
    # Generated furniture rows have different heights. Find the actual empty
    # transparent gutters, rather than cutting the bottom off a tall bookshelf.
    alpha = image.getchannel('A')
    active = [sum(alpha.crop((0, y, image.width, y + 1)).histogram()[128:]) > 12 for y in range(image.height)]
    runs, start = [], None
    for y, occupied in enumerate(active + [False]):
        if occupied and start is None: start = y
        if not occupied and start is not None:
            if runs and start - runs[-1][1] <= 5: runs[-1][1] = y
            else: runs.append([start, y])
            start = None
    if len(runs) != 4: raise ValueError(f'Expected four object rows, found {runs}')
    return [image.crop((round(column * image.width / 4), max(0, top - 2),
                        round((column + 1) * image.width / 4), min(image.height, bottom + 2)))
            for top, bottom in runs for column in range(4)]


def fit(image, size, padding=1, align='bottom'):
    image = clean(image)
    box = image.getchannel('A').getbbox()
    if box is None:
        raise ValueError('Empty generated sprite')
    sprite = image.crop(box)
    ratio = min((size[0] - 2 * padding) / sprite.width, (size[1] - 2 * padding) / sprite.height)
    sprite = sprite.resize((max(1, round(sprite.width * ratio)), max(1, round(sprite.height * ratio))), Image.Resampling.NEAREST)
    result = Image.new('RGBA', size)
    y = size[1] - padding - sprite.height if align == 'bottom' else (size[1] - sprite.height) // 2
    result.alpha_composite(sprite, ((size[0] - sprite.width) // 2, y))
    return result


def export():
    for folder in [PACK / 'tiles', PACK / 'props', PLAYER / 'Compatibility72', PLAYER / 'Frames/Walk', PLAYER / 'Frames/Idle']:
        folder.mkdir(parents=True, exist_ok=True)
    entries = {}
    def save(name, image, kind):
        path = PACK / kind / (name + '.png')
        image.save(path)
        w, h = image.size
        entries[name] = {'file': path.relative_to(MEDIA).as_posix(), 'size': [w, h],
                         'anchor': [w // 2, h - 1], 'category': kind}
        if kind == 'props' and name not in ['book_burgundy', 'book_teal', 'book_blue', 'book_ochre', 'open_book',
                                          'scrolls', 'parchment', 'ink_quill', 'candle', 'wall_sconce', 'hourglass',
                                          'brass_key', 'arched_window', 'wall_clock', 'book_sign']:
            entries[name]['collision'] = [max(2, w // 8), max(0, h - 20), max(8, w * 3 // 4), 16]

    tiles_source = Image.open(SOURCES / 'tiles-source.png').convert('RGBA')
    wall_source = Image.open(SOURCES / 'walls-source.png').convert('RGBA')
    for index, name in enumerate(FLOORS):
        sprite = cell(tiles_source, 8, 8, index)
        # The reference's first carpet square has an outside border. Its interior
        # provides a border-free center tile for longer carpet corridors.
        if name == 'carpet_center':
            w, h = sprite.size
            sprite = sprite.crop((round(w * .22), round(h * .22), round(w * .78), round(h * .78)))
        save(name, clean(sprite.resize((32, 32), Image.Resampling.NEAREST), opaque=True), 'tiles')
    for index, name in enumerate(STAIRS):
        save(name, clean(cell(tiles_source, 8, 8, 48 + index).resize((32, 32), Image.Resampling.NEAREST), opaque=True), 'tiles')
    for index, name in enumerate(WALLS):
        limits = [0, round(wall_source.height * .267), round(wall_source.height * .501), round(wall_source.height * .737), wall_source.height]
        column, row = index % 4, index // 4
        tile = wall_source.crop((round(column * wall_source.width / 4), limits[row],
                                 round((column + 1) * wall_source.width / 4), limits[row + 1]))
        # Preserve the 32x32 modules. Thin side-wall pieces retain their silhouette.
        tile = clean(tile.resize((32, 32), Image.Resampling.NEAREST))
        save(name, tile, 'tiles')
    for source_name, definitions in [('objects-source.png', FURNITURE), ('decor-source.png', DECOR)]:
        source = Image.open(SOURCES / source_name).convert('RGBA')
        cells = object_cells(source)
        for index, (name, size) in enumerate(definitions):
            small = name in ['book_burgundy', 'book_teal', 'book_blue', 'book_ochre', 'open_book', 'scrolls',
                             'parchment', 'ink_quill', 'candle', 'hourglass', 'brass_key']
            save(name, fit(cells[index], size, padding=8 if small else 1), 'props')

    # Exact 4x4 sprite-sheet structure, with one consistent scale and foot anchor
    # across BOTH animation states; no frame is independently stretched.
    raw_frames = {}
    for state, source_name in [('Walk', 'walk-source.png'), ('Idle', 'idle-source.png')]:
        source = Image.open(SOURCES / source_name).convert('RGBA')
        for index in range(16):
            frame = clean(cell(source, 4, 4, index))
            box = frame.getchannel('A').getbbox()
            if box is None: raise ValueError(f'Empty {state} frame {index}')
            raw_frames[state, index] = frame.crop(box)
    max_w = max(im.width for im in raw_frames.values())
    max_h = max(im.height for im in raw_frames.values())
    ratio = min(28 / max_w, 29 / max_h)
    for state, filename in [('Walk', 'Player.png'), ('Idle', 'Player_Idle.png')]:
        sheet = Image.new('RGBA', (128, 128))
        legacy = Image.new('RGBA', (288, 288))
        for index in range(16):
            frame = raw_frames[state, index]
            frame = frame.resize((max(1, round(frame.width * ratio)), max(1, round(frame.height * ratio))), Image.Resampling.NEAREST)
            output = Image.new('RGBA', (32, 32))
            output.alpha_composite(frame, ((32 - frame.width) // 2, 31 - frame.height))
            output = clean(output)
            output.save(PLAYER / f'Frames/{state}/{["Down", "Left", "Right", "Up"][index // 4]}_{index % 4}.png')
            sheet.alpha_composite(output, ((index % 4) * 32, (index // 4) * 32))
            legacy.alpha_composite(output.resize((64, 64), Image.Resampling.NEAREST), ((index % 4) * 72 + 4, (index // 4) * 72 + 4))
        sheet.save(PLAYER / filename)
        legacy.save(PLAYER / 'Compatibility72' / filename)

    # Pack the exported, already normalized sprites into deterministic atlases.
    tile_names = FLOORS + STAIRS + WALLS
    tile_atlas = Image.new('RGBA', (256, math.ceil(len(tile_names) / 8) * 32))
    for index, name in enumerate(tile_names):
        position = (index % 8 * 32, index // 8 * 32)
        tile_atlas.alpha_composite(Image.open(MEDIA / entries[name]['file']), position)
        entries[name]['atlas'] = {'file': 'Library_Tiles_32.png', 'rect': [*position, 32, 32]}
    tile_atlas.save(PACK / 'Library_Tiles_32.png')
    prop_atlas = Image.new('RGBA', (512, 1024))
    for index, (name, _) in enumerate(FURNITURE + DECOR):
        sprite = Image.open(MEDIA / entries[name]['file'])
        position = (index % 4 * 128 + (128 - sprite.width) // 2, index // 4 * 128 + 128 - sprite.height)
        prop_atlas.alpha_composite(sprite, position)
        entries[name]['atlas'] = {'file': 'Library_Objects.png', 'rect': [*position, sprite.width, sprite.height]}
    prop_atlas.save(PACK / 'Library_Objects.png')
    existing = json.loads((PACK / 'atlas.json').read_text(encoding='utf-8')) if (PACK / 'atlas.json').exists() else {}
    for name, entry in existing.get('assets', {}).items():
        if name not in entries and (MEDIA / entry['file']).exists():
            entries[name] = entry
    metadata = {'tileSize': 32, 'filter': 'nearest', 'alpha': 'binary', 'assets': entries,
                'player': {'walk': 'Player/Library32/Player.png', 'idle': 'Player/Library32/Player_Idle.png',
                           'frameSize': [32, 32], 'sheetSize': [128, 128], 'columns': 4,
                           'rows': ['Down', 'Left', 'Right', 'Up'], 'walkFrameSeconds': 0.1,
                           'idleFrameSeconds': 0.3, 'footAnchor': [16, 31], 'collision': [10, 23, 12, 8]}}
    (PACK / 'atlas.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (PLAYER / 'animations.json').write_text(json.dumps(metadata['player'], indent=2) + '\n', encoding='utf-8')
    print(f'Exported {len(tile_names)} tiles, {len(FURNITURE + DECOR)} props, 32 animation frames and 4 character sheets.')
    return metadata


if __name__ == '__main__':
    export()
