"""Checks for the shared Rainbow/mobile authoring contract."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('rpg_import', Path(__file__).parents[1] / 'import_rpg_assets.py')
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class RainbowImportTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.map = self.root / 'library.map'
        self.assets = {'floor': {'file': 'floor.png'}}
        self.scene = {'spawn': [32, 32], 'bounds': [0, 0, 256, 256]}

    def test_rainbow_decimal_save_and_crlf(self):
        self.map.write_bytes(b'BACKGROUND\r\nfloor,32.000000,64.000000\r\nFOREGROUND\r\nHITBOXES\r\nshelf,80,80;96,80;96,96;80,96\r\n')
        self.assertEqual(MODULE.validate_map(self.map, self.assets, self.scene),
                         {'BACKGROUND': 1, 'FOREGROUND': 0, 'HITBOXES': 1})

    def test_unknown_sprite_and_nonfinite_coordinates_fail_with_line(self):
        for row in ['missing,0,0', 'floor,NaN,0']:
            self.map.write_text('BACKGROUND\n' + row)
            with self.assertRaisesRegex(ValueError, ':2:'):
                MODULE.validate_map(self.map, self.assets)

    def test_blocked_spawn_is_rejected(self):
        self.map.write_text('BACKGROUND\nfloor,0,0\nHITBOXES\nshelf,40,50;60,50;60,70;40,70')
        with self.assertRaisesRegex(ValueError, 'spawn is blocked'):
            MODULE.validate_map(self.map, self.assets, self.scene)

    def test_logical_zone_overlapping_spawn_is_allowed(self):
        self.map.write_text('BACKGROUND\nfloor,0,0\nHITBOXES\nlogical:grandfather,40,50;60,50;60,70;40,70')
        counts = MODULE.validate_map(self.map, self.assets, self.scene)
        self.assertEqual(counts['HITBOXES'], 1)

    def test_incremental_write_preserves_timestamp(self):
        self.map.write_bytes(b'existing')
        original = self.map.stat().st_mtime_ns
        MODULE.write_if_changed(self.map, b'existing')
        self.assertEqual(original, self.map.stat().st_mtime_ns)
        MODULE.write_if_changed(self.map, b'changed')
        self.assertEqual(self.map.read_bytes(), b'changed')

    def test_invalid_map_preserves_imported_content(self):
        self.map.write_text('BACKGROUND\nunknown,0,0')
        self.scene.update({'map': 'library.map', 'atlas': 'atlas.json'})
        (self.root / 'scene.json').write_text(json.dumps(self.scene))
        (self.root / 'atlas.json').write_text(json.dumps({'assets': self.assets}))
        output = self.root / 'runtime'
        output.mkdir()
        preserved = output / 'keep.txt'
        preserved.write_text('untouched')
        with patch.object(MODULE, 'CONTENT', output):
            with self.assertRaisesRegex(ValueError, 'unknown sprite'):
                MODULE.import_assets(self.root, Path('scene.json'))
        self.assertEqual(list(output.iterdir()), [preserved])
        self.assertEqual(preserved.read_text(), 'untouched')


if __name__ == '__main__':
    unittest.main()
