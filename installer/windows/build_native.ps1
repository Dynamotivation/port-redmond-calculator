<#
The only step that needs Windows: compile the native calculator engine (MSVC, as
docs/PortableBuild.md describes) and the tiny signing bridge Inno calls under Wine on Linux.
Everything after this (publishing the managed app, signing, the Inno Setup installer, the
release, the website) runs on Linux, see installer/linux/package_windows.py.

Output, below -Out:
  native/calculator_engine.dll   the engine, injected into the Linux publish
  native/sign_bridge.exe         static-CRT helper that hands signing back to Linux
  crt/*.dll                      the app-local VC++ runtime the engine links against
#>
param([Parameter(Mandatory)][string]$Out)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Out = [IO.Path]::GetFullPath($Out, $root)
Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$Out\native", "$Out\crt", "$Out\obj" | Out-Null

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio with the C++ tools was not found.' }

# The Visual Studio generator keeps MSYS2's GCC or Clang from being picked by accident.
cmake -S $root -B "$root\build\windows-msvc" -G 'Visual Studio 17 2022' -A x64
if ($LASTEXITCODE) { throw 'Configuring the native engine failed.' }
cmake --build "$root\build\windows-msvc" --config Release --target CalculatorNative --parallel
if ($LASTEXITCODE) { throw 'Building the native engine failed.' }
$engine = "$root\build\windows-msvc\src\CalculatorNative\Release\calculator_engine.dll"
if (-not (Test-Path $engine)) { throw 'The native build produced no calculator_engine.dll.' }
Copy-Item $engine "$Out\native"

# The engine links the dynamic VC++ runtime; ship it beside the exe (app-local deployment)
# so the app starts on a machine without the redistributable.
$crt = Get-ChildItem "$vs\VC\Redist\MSVC\*\x64\Microsoft.VC*.CRT" -Directory |
  Sort-Object FullName | Select-Object -Last 1
if (-not $crt) { throw 'The VC++ runtime was not found.' }
Copy-Item "$($crt.FullName)\*.dll" "$Out\crt"

$build = "`"$vs\VC\Auxiliary\Build\vcvars64.bat`" && cl /nologo /EHsc /O2 /MT /Fo`"$Out\obj\sign_bridge.obj`" " +
  "/Fe`"$Out\native\sign_bridge.exe`" `"$root\installer\windows\sign_bridge.cpp`" ws2_32.lib"
cmd /c $build
if ($LASTEXITCODE) { throw 'Compiling the signing bridge failed.' }
Remove-Item "$Out\obj" -Recurse -Force
