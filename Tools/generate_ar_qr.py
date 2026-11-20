from pathlib import Path

import qrcode


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets" / "_Project" / "Art" / "Textures" / "QR"
LINKS = {
    "QR_Apollo_LRV.png": "lunarquest://exhibit/APOLLO_LRV",
    "QR_Yutu_Rover.png": "lunarquest://exhibit/YUTU_ROVER",
    "QR_Luna25_Capsule.png": "lunarquest://exhibit/LUNA25_CAPSULE",
}


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for filename, link in LINKS.items():
        qr = qrcode.QRCode(version=None, box_size=16, border=4)
        qr.add_data(link)
        qr.make(fit=True)
        image = qr.make_image(fill_color="black", back_color="white")
        image.save(OUTPUT / filename)
        print(f"{filename}: {link}")


if __name__ == "__main__":
    main()
