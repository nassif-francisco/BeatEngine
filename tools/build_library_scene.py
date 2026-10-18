"""Assemble the first room from the exported Library32 sprites, without painting art."""
from collections import deque
from pathlib import Path
import json
import random
from PIL import Image, ImageDraw, ImageFont

MEDIA = Path('C:/CommonMedia')
PACK = MEDIA / 'Environment/Packs/Library32'
DEST = MEDIA / 'Maps/Library32'


def build():
    DEST.mkdir(parents=True, exist_ok=True)
    metadata = json.loads((PACK / 'atlas.json').read_text(encoding='utf-8'))
    assets = metadata['assets']
    background, foreground, collisions = [], [], []
    rng = random.Random(32032)
    width, height = 42, 30
    def place(name, x, y, solid=False):
        foreground.append((name, x, y))
        if solid:
            w, h = assets[name]['size']
            if name in ['reading_table', 'reception_desk', 'book_cart']:
                body = [4, h // 3, w - 8, h * 2 // 3 - 4]
            else:
                body = assets[name].get('collision', [3, max(0, h - 24), w - 6, 22])
            bx, by, bw, bh = body
            collisions.append((name, x + bx, y + by, bw, bh))

    # Every floor cell is one native 32px tile. The central carpet and stone
    # cross-corridors leave wide routes between all shelving and reading areas.
    for y in range(height):
        for x in range(width):
            name = f'floor_wood_{rng.randrange(1, 9):02}'
            if x in [1, 2, 3, 38, 39, 40] or y in [3, 11, 18, 27]:
                name = f'floor_stone_{rng.randrange(1, 9):02}'
            if 19 <= x <= 22 and 4 <= y <= 27:
                name = 'carpet_center'
            background.append((name, x * 32, y * 32))
    for x in range(width):
        place('wall_n_02' if x % 6 == 0 else 'wall_n_01', x * 32, 32)
        place('wall_s', x * 32, (height - 1) * 32)
    for y in range(2, height - 1):
        place('wall_w', 0, y * 32)
        place('wall_e', (width - 1) * 32, y * 32)
    collisions += [('north_wall', 0, 0, width * 32, 64), ('south_wall', 0, (height - 1) * 32, width * 32, 32),
                   ('west_wall', 0, 0, 32, height * 32), ('east_wall', (width - 1) * 32, 0, 32, height * 32)]

    shelves = ['bookshelf_full', 'bookshelf_scrolls', 'bookshelf_rare']
    for i, x in enumerate([4, 8, 12, 27, 31, 35]):
        place(shelves[i % 3], x * 32, 2 * 32, True)
    for row in [6, 13, 20]:
        for i, column in enumerate([5, 10, 28, 33]):
            place(shelves[(i + row) % 3], column * 32, row * 32, True)
    for x, y in [(14, 9), (25, 9), (14, 16), (25, 16)]:
        place('reading_table', x * 32, y * 32, True)
        place('reading_chair', (x + .5) * 32, (y + 2) * 32, True)
        place('candle', (x + 1) * 32, (y + .1) * 32)
    for x, y in [(15, 23), (25, 23)]:
        place('armchair', x * 32, y * 32, True)
        place('standing_lamp', (x + 1) * 32, y * 32, True)
        place('book_stack', (x - 1) * 32, (y + 1) * 32, True)
    for x, y in [(18, 6), (23, 6), (18, 15), (23, 15)]:
        place('standing_lamp', x * 32, y * 32, True)
    for x in [17, 24]:
        place('globe', x * 32, 3 * 32, True)
    place('reception_desk', 20 * 32, 5 * 32, True)
    place('lectern', 21 * 32, 12 * 32, True)
    place('book_cart', 8 * 32, 25 * 32, True)
    place('ladder', 12 * 32, 12 * 32, True)
    place('bookshelf_low', 36 * 32, 25 * 32, True)
    place('double_door', 20 * 32, 27 * 32, True)
    for x in [2, 16, 25, 39]:
        place('arched_window', x * 32, 0)
    for x in [7, 13, 28, 34]:
        place('wall_sconce', x * 32, 32)
    place('wall_clock', 21 * 32, 0)
    place('book_sign', 19 * 32, 32)
    for x, y in [(2, 5), (39, 5), (2, 25), (39, 25)]:
        place('potted_fern', x * 32, y * 32, True)
    for name, x, y in [('book_teal', 7, 8), ('book_burgundy', 30, 15), ('parchment', 18, 21),
                       ('scrolls', 24, 22), ('book_ochre', 14, 25), ('open_book', 26, 25)]:
        place(name, x * 32, y * 32)

    scene = {'id': 'library32', 'name': 'La biblioteca', 'tileSize': 32, 'frameSize': 32,
             'viewHeight': 320, 'spawn': [656, 768], 'bounds': [32, 64, (width - 2) * 32, (height - 3) * 32],
             'map': 'Maps/Library32/library.map', 'atlas': 'Environment/Packs/Library32/atlas.json',
             'playerWalk': 'Player/Library32/Player.png', 'playerIdle': 'Player/Library32/Player_Idle.png',
             'ground': assets['floor_wood_01']['file'], 'backgroundTiles': len(background),
             'foregroundObjects': len(foreground), 'collisionRectangles': len(collisions)}
    # Check collision and connectivity with the same foot rectangle as the game.
    def walkable(x, y):
        left, top, right, bottom = x + 10, y + 23, x + 22, y + 31
        bx, by, bw, bh = scene['bounds']
        return (left >= bx and top >= by and right <= bx + bw and bottom <= by + bh and
                not any(left < cx + cw and right > cx and top < cy + ch and bottom > cy for _, cx, cy, cw, ch in collisions))
    assert walkable(*scene['spawn']), 'Spawn is blocked'
    start = (int(scene['spawn'][0] // 16), int(scene['spawn'][1] // 16))
    queue, visited = deque([start]), {start}
    while queue:
        x, y = queue.popleft()
        for next_cell in [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]:
            if next_cell not in visited and walkable(next_cell[0] * 16, next_cell[1] * 16):
                visited.add(next_cell)
                queue.append(next_cell)
    assert len(visited) > 2000, 'Library corridors are disconnected'
    for x, y in [(64, 192), (1216, 192), (240, 384), (1040, 384), (656, 288)]:
        assert (x // 16, y // 16) in visited, f'Blocked route to {(x, y)}'
    lines = ['BACKGROUND'] + [f'{name},{x:g},{y:g}' for name, x, y in background]
    lines += ['FOREGROUND'] + [f'{name},{x:g},{y:g}' for name, x, y in foreground]
    lines += ['HITBOXES'] + [f'{name},{x:g},{y:g};{x+w:g},{y:g};{x+w:g},{y+h:g};{x:g},{y+h:g}' for name, x, y, w, h in collisions]
    (DEST / 'library.map').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    (DEST / 'scene.json').write_text(json.dumps(scene, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    # A scene preview composed from the actual exported sprites, not concept art.
    preview = Image.new('RGBA', (width * 32, height * 32), '#211b23')
    cache = {name: Image.open(MEDIA / entry['file']).convert('RGBA') for name, entry in assets.items()}
    for name, x, y in background:
        preview.alpha_composite(cache[name], (int(x), int(y)))
    hero = Image.open(MEDIA / scene['playerIdle']).crop((0, 0, 32, 32))
    actors = [(y + cache[name].height, cache[name], int(x), int(y)) for name, x, y in foreground]
    actors.append((scene['spawn'][1] + 31, hero, *scene['spawn']))
    for _, image, x, y in sorted(actors, key=lambda actor: actor[0]):
        preview.alpha_composite(image, (int(x), int(y)))
    preview.save(PACK / 'Library_Scene_Preview.png')
    # Export an inventory contact sheet at readable integer pixel enlargement.
    catalog = Image.new('RGB', (1024, 1600), '#211b23')
    draw = ImageDraw.Draw(catalog)
    draw.text((24, 16), 'LIBRARY 32 - GAME ASSETS', fill='#f0d8b0')
    for index, (name, _) in enumerate(__import__('export_library_art').FURNITURE + __import__('export_library_art').DECOR):
        image = cache[name].resize((cache[name].width * 2, cache[name].height * 2), Image.Resampling.NEAREST)
        column, row = index % 8, index // 8
        # Tall furniture is displayed at native size to keep each contact cell independent.
        image = cache[name]
        x, y = 64 + column * 128 - image.width // 2, 56 + row * 156
        catalog.paste(image, (x, y + 100 - image.height), image)
        draw.text((column * 128 + 3, y + 105), name, fill='#e6cfac')
    catalog.paste(preview.resize((1008, 720), Image.Resampling.NEAREST).convert('RGB'), (8, 760))
    catalog.save(PACK / 'Asset_Catalog.png')
    print(f'Built a {width}x{height}-tile library: {len(foreground)} props/walls, {len(collisions)} collision rectangles, {len(visited)} reachable navigation samples.')


if __name__ == '__main__':
    build()
