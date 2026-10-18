"""Check the built APK contains only the library runtime content files."""
import argparse
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_APK = ROOT / 'BeatEngine/BeatEngine.Android/bin/Release/net9.0-android/com.rpgprototype.app-Signed.apk'
import json
MANIFEST = ROOT / 'BeatEngine/BeatEngine.Core/Content/Rpg/source-assets.json'
EXPECTED = {'assets/Content/' + (str(Path(name).with_suffix('.xnb')).replace('\\', '/') if name.endswith('.png') else name) for name in json.loads(MANIFEST.read_text())}
EXPECTED.add('assets/Content/Fonts/RpgHud.xnb')


def verify(apk):
    with ZipFile(apk) as archive:
        actual = {name for name in archive.namelist() if name.startswith('assets/Content/') and not name.endswith('/')}
        if actual != EXPECTED:
            raise AssertionError(f'Missing: {sorted(EXPECTED - actual)}; unexpected content: {len(actual - EXPECTED)} files')
        source_map = ROOT / 'BeatEngine/BeatEngine.Core/Content/Rpg/Maps/test.map'
        assert archive.read('assets/Content/Rpg/Maps/test.map') == source_map.read_bytes(), 'Map changed in packaging'
    print('PASS: APK has exactly the library content files and an unchanged authored map.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk', nargs='?', type=Path, default=DEFAULT_APK)
    verify(parser.parse_args().apk)
