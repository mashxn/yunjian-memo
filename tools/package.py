from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parent.parent
release = root / "release"
release.mkdir(exist_ok=True)
target = release / "云笺备忘录-v1.0.zip"

items = [
    root / "README.md",
    root / "install.bat",
    root / "install.ps1",
    root / "uninstall.bat",
    root / "uninstall.ps1",
]
items += sorted((root / "dist" / "YunJianMemo").glob("*"))
items += sorted((root / "docs").glob("*.png"))

with ZipFile(target, "w", ZIP_DEFLATED, compresslevel=9) as z:
    for item in items:
        arcname = item.relative_to(root).as_posix()
        z.write(item, arcname)
        print(arcname)
print(target)
print(f"{target.stat().st_size / 1024:.1f} KB")
