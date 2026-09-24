"""Package only runtime files and licences; never include local configuration or symbols."""
import argparse
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]


def package(name, files, output):
    output.mkdir(parents=True, exist_ok=True)
    target = output / name
    with ZipFile(target, 'w', compression=ZIP_DEFLATED) as archive:
        for entry, path in files.items():
            data = path.read_bytes()
            for marker in (b'/home/', b'/Users/', b'\\Users\\', b'/tmp/'):
                if marker.lower() in data.lower() or marker.decode().encode('utf-16le').lower() in data.lower():
                    raise ValueError(f'Private/local path marker in {entry}')
            info = ZipInfo(entry, date_time=(2026, 9, 24, 0, 0, 0))
            info.compress_type = ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data)
    with ZipFile(target) as archive:
        assert archive.testzip() is None
        assert set(archive.namelist()) == set(files)
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    target.with_suffix('.sha256').write_text(f'{digest}  {name}\n', encoding='utf-8')
    print(f'{name}: {digest}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts')
    parser.add_argument('--include-umbra', action='store_true')
    args = parser.parse_args()
    build = ROOT / 'RelicAtlas/bin/Release'
    manifest = json.loads((build / 'RelicAtlas.json').read_text(encoding='utf-8'))
    source_manifest = json.loads((ROOT / 'RelicAtlas/RelicAtlas.json').read_text(encoding='utf-8'))
    assert manifest['InternalName'] == 'RelicAtlas'
    assert manifest['DalamudApiLevel'] == 15
    assert manifest['AssemblyVersion'] == source_manifest['AssemblyVersion']
    files = {name: build / name for name in ('RelicAtlas.dll', 'RelicAtlas.json', 'RelicAtlas.deps.json')}
    files.update({'README.md': ROOT / 'README.md', 'LICENSE': ROOT / 'LICENSE',
                  'FONT-LICENSE.txt': ROOT / 'RelicAtlas/Assets/FONT-LICENSE.txt'})
    package(f"RelicAtlas-{manifest['AssemblyVersion']}.zip", files, args.output)
    if args.include_umbra:
        companion = ROOT / 'Umbra.RelicAtlas'
        files = {name: companion / 'bin/Release' / name
                 for name in ('Umbra.RelicAtlas.dll', 'Umbra.RelicAtlas.deps.json')}
        files.update({'README.md': companion / 'README.md', 'LICENSE': companion / 'LICENSE',
                      'CONTRACT-LICENSE.txt': ROOT / 'LICENSE'})
        package('Umbra.RelicAtlas-0.1.0.0.zip', files, args.output)


if __name__ == '__main__':
    main()
