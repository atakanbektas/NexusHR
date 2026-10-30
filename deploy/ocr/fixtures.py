"""Deterministic synthetic documents for opt-in native tests. No personal CV data."""
import base64
import io
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter


def document(kind):
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 30)
    image = Image.new("RGB", (1200, 1500), "white")
    draw = ImageDraw.Draw(image)
    text = ("Synthetic CV\nYazılım Geliştirici / Software Engineer\n"
            "Teknik Beceriler: C# PostgreSQL RabbitMQ\n"
            "Deneyim / Experience\nAcme | Engineer | 2020-01 | 2022-12\n"
            "Eğitim / Education: Teknik Üniversite\nDil / Language: English B2")
    if kind != "blank":
        draw.multiline_text((60, 80), text, fill="black", font=font, spacing=22)
    if kind == "low":
        image = image.resize((120, 150)).resize((1200, 1500)).filter(ImageFilter.GaussianBlur(5))
    output = io.BytesIO()
    if kind in ("scan", "multi"):
        image.save(output, "PDF", resolution=150, save_all=True, append_images=[image.copy()] if kind == "multi" else [])
    else:
        image.save(output, "JPEG" if kind == "jpg" else "WEBP" if kind == "webp" else "PNG")
    return output.getvalue()


if __name__ == "__main__":
    print(base64.b64encode(document(sys.argv[1])).decode())
