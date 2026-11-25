import io
import struct
import sys
from pathlib import Path

from PIL import Image

LARGEST_FRAME = 256


def pixels_only_png(path):
    with Image.open(path) as exported:
        if exported.width != exported.height:
            raise ValueError(f"{path.name} is {exported.width}x{exported.height}, an icon frame must be square")
        if exported.width > LARGEST_FRAME:
            raise ValueError(f"{path.name} is {exported.width} px, the largest .ico frame is {LARGEST_FRAME} px")
        frame = Image.new("RGBA", exported.size)
        frame.paste(exported.convert("RGBA"))
    encoded = io.BytesIO()
    frame.save(encoded, "PNG", optimize=True)
    return frame.width, encoded.getvalue()


def write_ico(frames_folder, ico_path):
    frames = sorted(pixels_only_png(path) for path in frames_folder.glob("*.png"))
    if not frames:
        raise ValueError(f"{frames_folder} has no .png files")
    sizes = [size for size, _ in frames]
    if len(set(sizes)) != len(sizes):
        raise ValueError(f"{frames_folder} has two frames of the same size: {sizes}")

    directory = struct.pack("<HHH", 0, 1, len(frames))
    offset = len(directory) + 16 * len(frames)
    for size, png in frames:
        directory += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(png), offset)
        offset += len(png)
    ico_path.write_bytes(directory + b"".join(png for _, png in frames))
    return sizes


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: python png_frames_to_ico.py <folder with one square .png per size> <output .ico>")
    written = write_ico(Path(sys.argv[1]), Path(sys.argv[2]))
    print(f"{sys.argv[2]}: {written} px, {Path(sys.argv[2]).stat().st_size} bytes")
