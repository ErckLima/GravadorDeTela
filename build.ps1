# Compila o Gravador de Tela em um único executável: dist\GravadorDeTela.exe
# Usa o compilador C# que já vem com o Windows (.NET Framework 4.x) - não precisa instalar nada.
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Compilador nao encontrado: $csc" }

New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null

$cscArgs = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
    "/out:$root\dist\GravadorDeTela.exe",
    "/win32manifest:$root\app.manifest",
    "/reference:$root\lib\ScreenRecorderLib.dll",
    "/resource:$root\lib\ScreenRecorderLib.dll,ScreenRecorderLib.dll",
    '/reference:System.dll', '/reference:System.Core.dll',
    '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    "/recurse:$root\src\*.cs"
)
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao (codigo $LASTEXITCODE)" }

$exe = Get-Item (Join-Path $root 'dist\GravadorDeTela.exe')
"OK: {0} ({1:N0} KB)" -f $exe.FullName, ($exe.Length / 1KB)
