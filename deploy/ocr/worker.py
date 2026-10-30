"""One disposable CPU/memory-bounded process per document; no network/model downloads."""
import base64
import csv
import io
import json
import os
import resource
import subprocess
import sys
import warnings
from PIL import Image, ImageOps

resource.setrlimit(resource.RLIMIT_AS, (768 * 1024 * 1024, 768 * 1024 * 1024))
resource.setrlimit(resource.RLIMIT_CPU, (70, 75))
resource.setrlimit(resource.RLIMIT_FSIZE, (40 * 1024 * 1024, 40 * 1024 * 1024))
Image.MAX_IMAGE_PIXELS = 16_000_000
warnings.simplefilter("error", Image.DecompressionBombWarning)
MAX_PAGES = 8


def run(args):
    return subprocess.check_output(args, stderr=subprocess.DEVNULL, timeout=25)


def extract(path, directory, validate=False):
    with open(path, "rb") as file:
        signature = file.read(16)
    files = []
    if signature.startswith(b"%PDF-"):
        info = run(["pdfinfo", path]).decode("utf-8", errors="replace")
        pages = next(int(line.split(":", 1)[1]) for line in info.splitlines() if line.startswith("Pages:"))
        if not 0 < pages <= MAX_PAGES or "Encrypted:       yes" in info:
            raise ValueError("limit")
        if validate:
            return {"valid": True}
        # Scale before raster allocation, independent of attacker-controlled MediaBox/DPI.
        prefix = os.path.join(directory, "page")
        run(["pdftoppm", "-scale-to", "1800", "-png", path, prefix])
        files = sorted((os.path.join(directory, x) for x in os.listdir(directory) if x.endswith(".png")),
            key=lambda x: int(os.path.basename(x).rsplit("-", 1)[1].split(".")[0]))
    elif signature.startswith(b"\x89PNG\r\n\x1a\n") or signature.startswith(b"\xff\xd8\xff") or (
            signature.startswith(b"RIFF") and signature[8:12] == b"WEBP"):
        files = [path]
    else:
        raise ValueError("signature")
    result = []
    total = 0
    for number, filename in enumerate(files, 1):
        with Image.open(filename) as source:
            if source.format not in ("PNG", "JPEG", "WEBP") or getattr(source, "n_frames", 1) != 1:
                raise ValueError("format")
            if source.width * source.height > Image.MAX_IMAGE_PIXELS:
                raise ValueError("pixels")
            if validate:
                source.load()
                return {"valid": True}
            image = ImageOps.exif_transpose(source).convert("RGB")
            image.thumbnail((1800, 1800))
            clean = os.path.join(directory, "ocr.png")
            image.save(clean, format="PNG")
            tsv = run(["tesseract", clean, "stdout", "-l", "tur+eng", "--psm", "3", "tsv"]).decode("utf-8")
            words, text, line = [], "", None
            for row in csv.DictReader(io.StringIO(tsv), delimiter="\t", quoting=csv.QUOTE_NONE):
                value = row.get("text", "").strip()
                if row.get("level") != "5" or not value:
                    continue
                current = (row["block_num"], row["par_num"], row["line_num"])
                if text:
                    text += "\n" if current != line else " "
                # .NET evidence offsets use UTF-16 code units, not Python code points.
                start = len(text.encode("utf-16-le")) // 2
                text += value
                words.append({"start": start, "length": len(value.encode("utf-16-le")) // 2,
                    "x": int(row["left"]), "y": int(row["top"]), "width": int(row["width"]),
                    "height": int(row["height"]), "confidence": max(0, min(100, float(row["conf"])))})
                line = current
            total += len(text)
            if total > 100_000 or len(words) > 10_000:
                raise ValueError("text limit")
            result.append({"number": number, "text": text, "width": image.width, "height": image.height,
                "words": words, "source": "tesseract-5-tur-eng"})
    return {"pages": result}


if __name__ == "__main__":
    print(json.dumps(extract(sys.argv[1], sys.argv[2], sys.argv[3] == "/validate"), ensure_ascii=False))
