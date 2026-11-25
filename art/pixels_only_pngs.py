import sys
from pathlib import Path

from png_frames_to_ico import pixels_only_png


def write_pixels_only_pngs(exports_folder, output_folder):
    exports = sorted(exports_folder.glob("*.png"))
    if not exports:
        raise ValueError(f"{exports_folder} has no .png files")
    output_folder.mkdir(parents=True, exist_ok=True)
    for export in exports:
        _, png = pixels_only_png(export)
        (output_folder / export.name).write_bytes(png)
    return [export.name for export in exports]


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: python pixels_only_pngs.py <folder with the exported .png files> <output folder>")
    written = write_pixels_only_pngs(Path(sys.argv[1]), Path(sys.argv[2]))
    print(f"{len(written)} files written to {sys.argv[2]}")
