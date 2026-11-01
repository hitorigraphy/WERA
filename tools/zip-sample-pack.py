"""samples/sample-pack 을 src/Wera.Web/wwwroot/sample-pack.wra 로 묶는다. 저장소 루트에서 실행한다: python tools/zip-sample-pack.py

zip 안의 항목 이름은 pack.tsv, master/<이름>_v1.tsv, 그리고 image 폴더(이미지 마스터가 가리키는 파일)를 최상위 폴더 없이 '/' 구분자로 넣는다.
"""
import os
import zipfile

src = 'samples/sample-pack'
out = 'src/Wera.Web/wwwroot/sample-pack.wra'

if os.path.exists(out):
    os.remove(out)
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    z.write(src + '/pack.tsv', 'pack.tsv')
    for folder in ('master', 'image'):
        root = os.path.join(src, folder)
        if not os.path.isdir(root):
            continue
        for dirpath, _, files in sorted(os.walk(root)):
            for f in sorted(files):
                path = os.path.join(dirpath, f)
                z.write(path, os.path.relpath(path, src).replace(os.sep, '/'))
print('wrote', out)
