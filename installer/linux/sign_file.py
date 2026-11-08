"""Mandatory Authenticode signing, also used by Inno Setup through Wine."""

import os
from pathlib import Path
import struct
import subprocess
import sys

PUBLISHER_URL = "https://github.com/Dynamotivation/port-redmond-calculator"
PRODUCT = "Redmond Calculator"


def is_signed(path):
    """True when the PE file already carries an Authenticode signature (certificate table)."""
    with open(path, "rb") as stream:
        header = stream.read(4096)
        if header[:2] != b"MZ":
            return False
        pe = struct.unpack_from("<I", header, 0x3C)[0]
        stream.seek(pe)
        nt = stream.read(24 + 240)
    if nt[:4] != b"PE\0\0":
        return False
    magic = struct.unpack_from("<H", nt, 24)[0]
    directories = 24 + (112 if magic == 0x20B else 96)
    # Data directory 4 is the certificate table; a nonzero size means a signature exists.
    return struct.unpack_from("<II", nt, directories + 4 * 8)[1] != 0


def sign(path):
    path = str(path)
    if ":" in path:
        path = subprocess.check_output(["winepath", "-u", path], text=True).strip()
    source = Path(path).resolve(strict=True)
    destination = source.with_name(source.name + ".signed")
    try:
        subprocess.run([
            "osslsigncode", "sign", "-pkcs12", os.environ["SIGNING_PFX_PATH"],
            "-readpass", os.environ["SIGNING_PASSWORD_PATH"], "-h", "sha256",
            "-n", PRODUCT, "-i", PUBLISHER_URL,
            "-ts", "http://timestamp.digicert.com", "-in", str(source),
            "-out", str(destination),
        ], check=True)
        subprocess.run([
            "osslsigncode", "verify", "-CAfile", os.environ["SIGNING_CA_PATH"],
            "-in", str(destination),
        ], check=True)
        destination.replace(source)
    finally:
        destination.unlink(missing_ok=True)


if __name__ == "__main__":
    sign(sys.argv[1])
