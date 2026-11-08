"""Sign the Windows bundle and compile the signed Inno Setup installer on Linux.

The native engine is built on the Windows runner (installer/windows/build_native.ps1); the
managed app is published for win-x64 on Linux into build/installer/bundle. This script then:

1. signs every binary of the bundle that carries no signature yet (Microsoft's own files keep
   theirs), 2. compiles installer/redmond-calculator.iss with Inno Setup 5.6.1 under Wine, which
   also signs the uninstaller and the setup through the same signer, and 3. verifies the result
   and writes the file's SHA-256.
"""

import base64
import hashlib
import os
from pathlib import Path
import re
import secrets
import socketserver
import subprocess
import tempfile
import threading
import urllib.request

from sign_file import is_signed, sign

ROOT = Path(__file__).resolve().parents[2]
BUILD = ROOT / "build/installer"
PLATFORM = "windows-x64"


def run(*args):
    subprocess.run([str(arg) for arg in args], check=True)


def main():
    # Fail before downloading tools or compiling anything if signing is absent.
    pfx = os.environ.get("SIGNING_CERT_PFX_BASE64")
    password = os.environ.get("SIGNING_CERT_PASSWORD")
    if not pfx or not password:
        raise SystemExit("Both signing secrets are required; unsigned output is forbidden.")
    bundle = BUILD / "bundle"
    if not (bundle / "Redmond Calculator.exe").is_file() or not (bundle / "calculator_engine.dll").is_file():
        raise SystemExit("The bundle (build/installer/bundle) needs Redmond Calculator.exe and calculator_engine.dll.")

    with tempfile.TemporaryDirectory(prefix="signing-", dir=BUILD) as temp:
        secure = Path(temp)
        secure.chmod(0o700)
        certificate = secure / "certificate.pfx"
        certificate.write_bytes(base64.b64decode(pfx, validate=True))
        password_file = secure / "password"
        password_file.write_text(password)
        certificate.chmod(0o600)
        password_file.chmod(0o600)
        os.environ["SIGNING_PFX_PATH"] = str(certificate)
        os.environ["SIGNING_PASSWORD_PATH"] = str(password_file)
        trust = secure / "trust.pem"
        run("openssl", "pkcs12", "-in", certificate, "-passin", f"file:{password_file}",
            "-nokeys", "-clcerts", "-out", trust)
        # The publisher certificate is self-signed. Explicitly trust that certificate for
        # integrity checks and the system roots for timestamping.
        with trust.open("ab") as output:
            output.write(Path("/etc/ssl/certs/ca-certificates.crt").read_bytes())
        os.environ["SIGNING_CA_PATH"] = str(trust)

        binaries = sorted(path for path in bundle.rglob("*")
                          if path.suffix.lower() in {".exe", ".dll"} and not is_signed(path))
        print(f"Signing {len(binaries)} unsigned binaries.", flush=True)
        for binary in binaries:
            sign(binary)
        for required in ("Redmond Calculator.exe", "calculator_engine.dll"):
            run("osslsigncode", "verify", "-CAfile", trust, "-in", bundle / required)

        tools = BUILD / "inno"
        compiler = tools / "app/ISCC.exe"
        if not compiler.exists():
            installer = BUILD / "innosetup-5.6.1-unicode.exe"
            urllib.request.urlretrieve(
                "https://files.jrsoftware.org/is/5/innosetup-5.6.1-unicode.exe", installer)
            expected = "27d49e9bc769e9d1b214c153011978db90dc01c2acd1ddcd9ed7b3fe3b96b538"
            if hashlib.sha256(installer.read_bytes()).hexdigest() != expected:
                raise SystemExit("Inno Setup download hash mismatch.")
            run("innoextract", "--silent", "--output-dir", tools, installer)

        version = re.search(r"<Version>\s*(\d+\.\d+\.\d+)\s*</Version>",
                            (ROOT / "Directory.Build.props").read_text(encoding="utf-8"))[1]
        (ROOT / "dist").mkdir(exist_ok=True)
        source = subprocess.check_output(["winepath", "-w", str(bundle)], text=True).strip()
        dist = subprocess.check_output(["winepath", "-w", str(ROOT / "dist")], text=True).strip()
        iss = subprocess.check_output(
            ["winepath", "-w", str(ROOT / "installer/redmond-calculator.iss")], text=True).strip()
        token = secrets.token_hex(32)

        class SignRequest(socketserver.StreamRequestHandler):
            def handle(self):
                self.connection.settimeout(180)
                if self.rfile.readline(256).decode().strip() != token:
                    self.wfile.write(b"NO\n")
                    return
                filename = self.rfile.readline(32768).decode().strip()
                try:
                    sign(filename)
                    self.wfile.write(b"OK\n")
                except Exception as error:
                    print(f"Signing callback failed: {error}", flush=True)
                    self.wfile.write(b"NO\n")

        # The PE bridge returns nonzero unless the Linux signer completes and verifies the
        # signature. Inno also signs the uninstaller and temporary setup copies through it.
        with socketserver.TCPServer(("127.0.0.1", 0), SignRequest) as server:
            os.environ["SIGN_PORT"] = str(server.server_address[1])
            os.environ["SIGN_TOKEN"] = token
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            bridge = subprocess.check_output(
                ["winepath", "-w", str(BUILD / "native/sign_bridge.exe")], text=True).strip()
            try:
                run("wine", compiler, f"/DAppVersion={version}", f"/DSourceDir={source}",
                    "/DSignEnabled", f"/DOutputDir={dist}", f'/Sdyn=$q{bridge}$q $f', iss)
            finally:
                server.shutdown()
                thread.join()

        # Inno names the file <slug>-setup-<version>.exe; the platform and architecture go on the end.
        built = ROOT / f"dist/redmond-calculator-setup-{version}.exe"
        setup = ROOT / f"dist/redmond-calculator-setup-{version}-{PLATFORM}.exe"
        built.replace(setup)
        run("osslsigncode", "verify", "-CAfile", trust, "-in", setup)
        digest = hashlib.sha256(setup.read_bytes()).hexdigest()
        setup.with_suffix(".exe.sha256").write_text(f"{digest}  {setup.name}\n")


if __name__ == "__main__":
    main()
