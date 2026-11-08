"""Renders the Inno Setup wizard artwork for a Dynamotivation app installer.

Generates the per-DPI wizard BMP set and the multi-size setup.ico for
Inno Setup 5.6.1 from the SVG masters in assets/, using the brand identity of
dynamotivation.com:

  --main-color #ff4c4a, --main-color-dark #893750,
  accent gradient #3dffe5 -> #38e89f -> #4aff80, Rubik type.

Every app supplies its own masters so the renderer stays app-agnostic:
  assets/setup-icon.svg   the installer mark (the .ico's inner drawing)
  assets/app-icon.svg     optional: the same mark without the download badge. When
                          present it replaces setup-icon.svg in the wizard artwork
                          (side panel, corner glyph) and also renders assets/app.ico,
                          the badge-free icon for the app exe; setup.ico keeps the badge.
  assets/full-logo.svg    the product roundel for the side panel ('double' layout)
  assets/logo.<png|jpg|webp>  optional raster logo; builds a full-logo.svg
                              substitute automatically ('single' layout)

Requires: Pillow, fonttools+brotli (pip) and resvg.exe (see --resvg).
Usage:
    py tools\\render_assets.py [--assets PATH] [--font-dir PATH]
        [--app-icon apps\\windows\\windows\\runner\\resources\\app_icon.ico]
        [--layout double|single] [--wordmark "Name"] [--subtitle "Kind"]
Writes assets/setup.ico, assets/wizard-large-{100..250}.bmp and
assets/wizard-small-{100..250}.bmp (24-bit BMP, Inno 5.6).
"""

from __future__ import annotations

import argparse
import base64
import subprocess
import sys
from pathlib import Path

from PIL import Image

ASSETS = Path(__file__).resolve().parent.parent / "assets"

# Inno 5.6 wizard image areas (pre-6.6 scaling): side panel is 164:314.
LARGE_SIZES = [(164, 314), (202, 386), (240, 459), (290, 556), (315, 604), (366, 700), (416, 797)]
SMALL_SIZES = [(58, 58), (71, 71), (85, 85), (103, 103), (112, 112), (129, 129), (147, 147)]

GRADIENT_DEFS = (
    '<linearGradient id="stem" x1="0" y1="0" x2="1" y2="1">'
    '<stop offset="0" stop-color="#3dffe5"/><stop offset="1" stop-color="#4aff80"/></linearGradient>'
    '<linearGradient id="badge" x1="0" y1="0" x2="1" y2="1">'
    '<stop offset="0" stop-color="#CC3C3B"/><stop offset="1" stop-color="#893750"/></linearGradient>'
)


def inline_outer_svg(path: Path) -> str:
    """Strip the <svg> wrapper and rename gradient ids so inlining cannot collide."""
    inner = path.read_text(encoding="utf-8")
    inner = inner.split("<svg", 1)[1].split(">", 1)[1].rsplit("</svg>", 1)[0]
    return inner.replace('id="a"', 'id="brandstem"').replace('url(#a)', 'url(#brandstem)')


def installer_mark(assets: Path) -> str:
    """The installer mark, taken from the app's editable setup-icon.svg master."""
    return inline_outer_svg(assets / "setup-icon.svg")


def wizard_mark(assets: Path) -> str:
    """The mark shown in the wizard: app-icon.svg (no badge) when supplied, else the installer mark."""
    app_icon = assets / "app-icon.svg"
    return inline_outer_svg(app_icon) if app_icon.exists() else installer_mark(assets)


def raster_logo_svg(image_path: Path, tmp: Path) -> str:
    """Wrap a raster logo (PNG/WebP/JPEG) into a square SVG the renderer can inline."""
    im = Image.open(image_path).convert("RGBA")
    tmp.mkdir(parents=True, exist_ok=True)
    png = tmp / "logo.png"
    im.save(png)
    data = base64.b64encode(png.read_bytes()).decode("ascii")
    print(f"inlined raster logo {im.size} from {image_path.name} as an SVG master")
    return (
        '<svg xmlns="http://www.w3.org/2000/svg" width="315.23" height="315.23" viewBox="0 0 315.23 315.23">'
        '<rect width="315.23" height="315.23" fill="#ffffff"/>'
        '<image x="24.5" y="24.5" width="266.23" height="266.23" preserveAspectRatio="xMidYMid meet" '
        f'href="data:image/png;base64,{data}"/></svg>'
    )


def roundel_inner(assets: Path, args: argparse.Namespace, tmp: Path) -> str | None:
    """Inner content of the (optional) upper product roundel."""
    if args.layout == "single":
        return None
    roundel = assets / "full-logo.svg"
    if not roundel.exists():
        # The publisher mark is shared across all apps: fall back to the toolkit's copy.
        toolkit_roundel = Path(__file__).resolve().parent.parent / "assets" / "full-logo.svg"
        if toolkit_roundel.exists():
            roundel = toolkit_roundel
        else:
            roundel = None
    if roundel is not None:
        return inline_outer_svg(roundel)
    raster = args.logo_image
    if (assets / "logo.png").exists():
        raster = raster or assets / "logo.png"
    if (assets / "logo.webp").exists():
        raster = raster or assets / "logo.webp"
    if raster:
        (assets / "full-logo.svg").write_text(raster_logo_svg(Path(raster), tmp), encoding="utf-8")
        return inline_outer_svg(assets / "full-logo.svg")
    sys.exit(f"layout '{args.layout}' needs {assets / 'full-logo.svg'}, or the publisher logo in the toolkit, or a raster logo")


def large_svg(assets: Path, args: argparse.Namespace, tmp: Path) -> str:
    """164x314 side panel: roundel above the installer mark, burgundy line, Rubik wordmark."""
    inner = roundel_inner(assets, args, tmp)
    mark = 78  # upper and lower marks render at the same size
    if inner is None:
        top = f'<g transform="translate(26,36) scale(1.16)">{wizard_mark(assets)}</g>'
        underline = ('<rect x="47" y="238" width="70" height="5" rx="2.5" fill="url(#badge)"/>')
        name_y = 269
        sub_y = 289
    else:
        top = (
            f'<g transform="translate(43,32) scale({78 / 315.23:.6f})">{inner}</g>'
            f'<g transform="translate(43,134) scale({mark / 96:.6f})">{wizard_mark(assets)}</g>'
        )
        underline = '<rect x="47" y="226" width="70" height="5" rx="2.5" fill="url(#badge)"/>'
        name_y = 257
        sub_y = 277
    subtitle = (
        f'\n  <text x="82" y="{sub_y}" font-family="Rubik" font-weight="400" font-size="10" fill="#893750" text-anchor="middle">{args.subtitle}</text>'
        if args.subtitle
        else ""
    )
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="164" height="314" viewBox="0 0 164 314">
  <defs>{GRADIENT_DEFS}</defs>
  <rect width="164" height="314" fill="#ffffff"/>
  {top}
  {underline}
  <text x="82" y="{name_y}" font-family="Rubik" font-weight="700" font-size="13.5" fill="#2c2c2c" text-anchor="middle">{args.wordmark}</text>{subtitle}
</svg>"""


def small_svg(assets: Path, args: argparse.Namespace) -> str:
    """Square corner glyph on white, matching the wizard's white surface."""
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="147" height="147" viewBox="0 0 147 147">
  <defs>{GRADIENT_DEFS}</defs>
  <rect width="147" height="147" fill="#ffffff"/>
  <g transform="translate(11,11) scale(1.30)">{wizard_mark(assets)}</g>
</svg>"""


FONT_DIR_DEFAULT = Path(r"D:\tools\resvg\fonts")
RESVG_DEFAULT = r"D:\tools\resvg\resvg.exe"


def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--resvg", default=RESVG_DEFAULT, help="path to resvg.exe")
    ap.add_argument("--font-dir", type=Path, default=FONT_DIR_DEFAULT,
                    help="directory with Rubik-Regular.ttf and Rubik-Bold.ttf")
    ap.add_argument("--assets", type=Path, default=ASSETS, help="assets output directory")
    ap.add_argument("--app-icon", type=Path, default=None,
                    help="optional app icon destination; receives a copy of setup.ico "
                         "(e.g. apps/windows/windows/runner/resources/app_icon.ico)")
    ap.add_argument("--layout", choices=("double", "single"), default="double",
                    help="double: roundel above the installer mark; single: one large mark")
    ap.add_argument("--wordmark", default="Dynamotivation", help="main wordmark line on the side panel")
    ap.add_argument("--subtitle", default="Software Hub", help="second wordmark line (empty string hides it)")
    ap.add_argument("--logo-image", type=Path, default=None,
                    help="raster logo to wrap for the side panel roundel")
    return ap.parse_args()


def convert_fonts(font_dir: Path) -> str:
    """Validate the Rubik font dir (Rubik-Regular.ttf / Rubik-Bold.ttf)."""
    if not (font_dir / "Rubik-Bold.ttf").exists():
        sys.exit(f"missing Rubik fonts in {font_dir}. Instance Rubik 400/700 from "
                 "google/fonts's Rubik[wght].ttf with fontTools.varLib.instancer and "
                 'name the family "Rubik" (see README).')
    return str(font_dir)


def render(svg_path: Path, out_png: Path, resvg: str, font_dir: str, size: tuple[int, int]) -> None:
    subprocess.run(
        [resvg, "--width", str(size[0]), "--height", str(size[1]), "--use-fonts-dir", font_dir, str(svg_path), str(out_png)],
        check=True,
    )


def to_bmp(png_path: Path, bmp_path: Path, size: tuple[int, int], master: tuple[int, int]) -> None:
    im = Image.open(png_path).convert("RGB")
    if size != master:
        im = im.resize(size, Image.LANCZOS)
    im.save(bmp_path)


def main() -> None:
    args = parse_args()
    assets = args.assets
    tmp = assets / ".render-tmp"
    tmp.mkdir(exist_ok=True)
    font_dir = convert_fonts(args.font_dir)

    # Installer icon, as a multi-size .ico, from the app's editable master.
    master = assets / "setup-icon.svg"
    icon_png = tmp / "setup-icon.png"
    subprocess.run([args.resvg, "--width", "256", "--height", "256", "--use-fonts-dir", font_dir, str(master), str(icon_png)], check=True)
    icon = Image.open(icon_png)
    icon.save(assets / "setup.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    # The app's own icon (no download badge) for the exe, from the optional app-icon.svg.
    app_master = assets / "app-icon.svg"
    if app_master.exists():
        app_png = tmp / "app-icon.png"
        subprocess.run([args.resvg, "--width", "256", "--height", "256", "--use-fonts-dir", font_dir, str(app_master), str(app_png)], check=True)
        Image.open(app_png).save(assets / "app.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
        print("app.ico")
    if args.app_icon:
        args.app_icon.parent.mkdir(parents=True, exist_ok=True)
        args.app_icon.write_bytes((assets / "setup.ico").read_bytes())
        print("setup.ico +", args.app_icon.name)
    else:
        print("setup.ico")

    for name, svg_text, sizes in (
        ("wizard-large", large_svg(assets, args, tmp), LARGE_SIZES),
        ("wizard-small", small_svg(assets, args), SMALL_SIZES),
    ):
        svg_file = tmp / f"{name}.svg"
        svg_file.write_text(svg_text, encoding="utf-8")
        master = (sizes[-1][0] * 2, sizes[-1][1] * 2)  # render 2x of the largest, then downscale
        render(svg_file, tmp / f"{name}-master.png", args.resvg, font_dir, master)
        dpi_map = (
            {58: 100, 71: 125, 85: 150, 103: 175, 112: 200, 129: 225, 147: 250}
            if name == "wizard-small"
            else {164: 100, 202: 125, 240: 150, 290: 175, 315: 200, 366: 225, 416: 250}
        )
        for size in sizes:
            to_bmp(tmp / f"{name}-master.png", assets / f"{name}-{dpi_map[size[0]]}.bmp", size, master)
            print(f"{name}-{dpi_map[size[0]]}.bmp {size}")

    print("done")


if __name__ == "__main__":
    main()
