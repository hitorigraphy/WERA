"""모드를 묶은 바깥 게임팩(wrapper)을 만든다: python tools/zip-wrapper-pack.py <폴더> <출력.wra>

폴더에는 pack.tsv(id, title), master/pack_master_v1.tsv(order_number, pack_file)와 중첩 팩 파일들(예: packs/main.wra, packs/mod1.wra)이 있어야 한다.
pack_file은 이 폴더 안의 상대 경로(항상 '/')로 적는다. 폴더 안의 모든 파일을 최상위 폴더 없이 그 상대 경로 그대로 zip에 넣는다
(이미 압축된 .wra/.zip은 다시 압축하지 않고 그대로 넣는다). 만든 뒤 pack_master가 가리키는 파일이 모두 있는지 확인한다.

예) 폴더 구조
    pack.tsv                        id, title
    master/pack_master_v1.tsv       order_number<TAB>pack_file<TAB>memo
    packs/main.wra                  기본 게임팩
    packs/mod1.wra                  모드 1
"""
import os
import sys
import zipfile

for stream in (sys.stdout, sys.stderr):
    if hasattr(stream, 'reconfigure'):
        stream.reconfigure(encoding='utf-8')      # 콘솔 코드 페이지와 상관없이 한글 안내가 깨지지 않게


def main(folder: str, out: str) -> int:
    if not os.path.isfile(os.path.join(folder, 'pack.tsv')):
        print('오류: pack.tsv가 없습니다:', folder)
        return 1
    master = os.path.join(folder, 'master', 'pack_master_v1.tsv')
    if not os.path.isfile(master):
        print('오류: master/pack_master_v1.tsv가 없습니다:', folder)
        return 1

    names = []
    for dirpath, _, files in sorted(os.walk(folder)):
        for f in sorted(files):
            path = os.path.join(dirpath, f)
            names.append((path, os.path.relpath(path, folder).replace(os.sep, '/')))
    if os.path.exists(out):
        os.remove(out)
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        for path, name in names:
            compression = zipfile.ZIP_STORED if name.lower().endswith(('.wra', '.zip')) else zipfile.ZIP_DEFLATED
            z.write(path, name, compress_type=compression)

    present = {name for _, name in names}
    with open(master, encoding='utf-8-sig') as fh:
        rows = [line.rstrip('\r\n').split('\t') for line in fh if line.strip()]
    missing = [r[1].strip() for r in rows[1:] if len(r) > 1 and r[1].strip() not in present]
    if missing:
        print('경고: pack_master가 가리키지만 폴더에 없는 파일:', ', '.join(missing))
    print('wrote', out, '(중첩 팩', len(rows) - 1, '개)')
    return 0


if __name__ == '__main__':
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1], sys.argv[2]))
