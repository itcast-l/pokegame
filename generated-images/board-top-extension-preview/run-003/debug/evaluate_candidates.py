from PIL import Image
import numpy as np
from pathlib import Path

base = Image.open('/private/var/folders/h5/lzlqgf4x4wn0mjw0pc36trpc0000gn/T/codex-clipboard-d1e4926d-040b-4bbf-ad04-0c048c59b3af.png').convert('RGB')
base_np = np.asarray(base, dtype=np.int16)
paths = [
    Path('generated-images/board-top-extension-preview/run-003/output/candidate-a.png'),
    Path('generated-images/board-top-extension-preview/run-003/output/candidate-b.png'),
]
for path in paths:
    if not path.exists():
        print(path.name, 'missing')
        continue
    img = Image.open(path).convert('RGB').resize(base.size, Image.Resampling.LANCZOS)
    arr = np.asarray(img, dtype=np.int16)
    diff = np.abs(arr - base_np).sum(axis=2)
    top = diff[:120, :]
    mid = diff[120:220, :]
    low = diff[220:, :]
    print(path.name)
    print('  top_mean', float(top.mean()))
    print('  mid_mean', float(mid.mean()))
    print('  low_mean', float(low.mean()))
    print('  low_changed_px_gt30', int((low > 30).sum()))
    print('  mid_changed_px_gt30', int((mid > 30).sum()))
