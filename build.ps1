$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDir = Join-Path $projectDir "build"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The Windows .NET Framework C# compiler was not found."
}

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$references = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll"
)

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32manifest:"$projectDir\app.manifest" /win32icon:"$projectDir\PhoneBridge.ico" `
    $references /out:"$buildDir\PhoneBridge.exe" `
    "$projectDir\Program.cs" "$projectDir\Core.cs" "$projectDir\MainForm.cs"
if ($LASTEXITCODE -ne 0) { throw "PhoneBridge build failed." }

& $compiler /nologo /target:exe /optimize+ /platform:anycpu `
    $references /out:"$buildDir\PhoneBridge.Tests.exe" `
    "$projectDir\Core.cs" "$projectDir\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw "PhoneBridge test build failed." }

& "$buildDir\PhoneBridge.Tests.exe"
if ($LASTEXITCODE -ne 0) { throw "PhoneBridge tests failed." }

Write-Host "Built: $buildDir\PhoneBridge.exe"
