"""Captures the Inno Setup wizard window to a PNG (QA helper, not shipped).

Usage: py installer\\tools\\capture_window.py "<window title>" out.png
DPI-aware, works while another window covers the wizard (PrintWindow).
"""

from __future__ import annotations

import ctypes
import struct
import sys
from pathlib import Path

from PIL import Image

class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                ("right", ctypes.c_long), ("bottom", ctypes.c_long)]

class BIH(ctypes.Structure):
    _fields_ = [("biSize", ctypes.c_uint), ("biWidth", ctypes.c_int),
                ("biHeight", ctypes.c_int), ("biPlanes", ctypes.c_ushort),
                ("biBitCount", ctypes.c_ushort), ("biCompression", ctypes.c_uint),
                ("biSizeImage", ctypes.c_uint), ("biXPelsPerMeter", ctypes.c_int),
                ("biYPelsPerMeter", ctypes.c_int), ("biClrUsed", ctypes.c_uint),
                ("biClrImportant", ctypes.c_uint)]

def main() -> None:
    ctypes.windll.user32.SetProcessDPIAware()
    title, out = sys.argv[1], sys.argv[2]
    u32, g32 = ctypes.windll.user32, ctypes.windll.gdi32
    hwnd = u32.FindWindowW(None, title)
    if not hwnd:
        sys.exit(f"window not found: {title!r}")
    rect = RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(rect))
    w, h = rect.right - rect.left, rect.bottom - rect.top
    window_dc = u32.GetWindowDC(hwnd)
    mem_dc = g32.CreateCompatibleDC(window_dc)
    bitmap = g32.CreateCompatibleBitmap(window_dc, w, h)
    g32.SelectObject(mem_dc, bitmap)
    u32.PrintWindow(hwnd, mem_dc, 2)  # PW_RENDERFULLCONTENT
    bi = BIH(ctypes.sizeof(BIH), w, -h, 1, 32, 0, 0, 0, 0, 0, 0)
    buf = ctypes.create_string_buffer(w * h * 4)
    g32.GetDIBits(mem_dc, bitmap, 0, h, buf, ctypes.byref(bi), 0)
    Image.frombuffer("RGB", (w, h), buf.raw, "raw", "BGRX").save(out)
    print(f"saved {Path(out)} {w}x{h}")

if __name__ == "__main__":
    main()
