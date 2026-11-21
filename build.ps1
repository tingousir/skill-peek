# Builds SkillPeek with the csc.exe that ships with Windows — no .NET SDK required.

$ErrorActionPreference = "Stop"
$dist = Join-Path $PSScriptRoot "dist"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe not found (.NET Framework 4.x is required)." }

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$exe = Join-Path $dist "SkillPeek.exe"
$staged = Join-Path $dist "SkillPeek.build.exe"
$manifest = Join-Path $PSScriptRoot "src\app.manifest"
$catalog = Join-Path $PSScriptRoot "ui\skills.tsv"

try {
    & $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
        "/win32manifest:$manifest" "/resource:$catalog,SkillPeek.skills.tsv" "/out:$staged" `
        /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        (Join-Path $PSScriptRoot "src\App.cs") (Join-Path $PSScriptRoot "src\SkillPanel.cs")
    if ($LASTEXITCODE -ne 0) { throw "Compile failed." }

    if (Test-Path -LiteralPath $exe) { [IO.File]::Replace($staged, $exe, [NullString]::Value) }
    else { Move-Item -LiteralPath $staged -Destination $exe }
} finally {
    if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged }
}

foreach ($legacy in @("Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.WinForms.dll", "WebView2Loader.dll", "ui\index.html")) {
    $path = Join-Path $dist $legacy
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        try { Remove-Item -LiteralPath $path }
        catch { Write-Warning "Could not remove old build file: $path" }
    }
}
$oldUi = Join-Path $dist "ui"
if ((Test-Path -LiteralPath $oldUi -PathType Container) -and !(Get-ChildItem -LiteralPath $oldUi -Force)) {
    Remove-Item -LiteralPath $oldUi
}

Get-Item -LiteralPath $exe | Select-Object FullName, Length
