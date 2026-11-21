# Builds SkillPeek with the csc.exe that ships with Windows — no .NET SDK required.
# Usage: .\build.ps1 [-Version 0.1.1]

param([string]$Version = "0.1.0")
$Version = $Version.TrimStart("v", "V")

$ErrorActionPreference = "Stop"
$dist = Join-Path $PSScriptRoot "dist"
$build = Join-Path $PSScriptRoot "build"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe not found (.NET Framework 4.x is required)." }

New-Item -ItemType Directory -Force -Path $dist, $build | Out-Null
$exe = Join-Path $dist "SkillPeek.exe"
$staged = Join-Path $dist "SkillPeek.build.exe"
$manifest = Join-Path $PSScriptRoot "src\app.manifest"
$catalog = Join-Path $PSScriptRoot "ui\skills.tsv"

# 版本信息随构建生成，仓库里不留版本号副本
$versionFile = Join-Path $build "VersionInfo.cs"
$parts = ($Version -split '\.') + @("0", "0", "0")
$asm = "$($parts[0]).$($parts[1]).$($parts[2]).$($parts[3])"
@"
using System.Reflection;
[assembly: AssemblyTitle("Skill Peek")]
[assembly: AssemblyDescription("Hotkey-triggered skill panel for Windows")]
[assembly: AssemblyProduct("Skill Peek")]
[assembly: AssemblyCompany("Skill Peek")]
[assembly: AssemblyCopyright("MIT Licensed")]
[assembly: AssemblyVersion("$asm")]
[assembly: AssemblyFileVersion("$asm")]
[assembly: AssemblyInformationalVersion("$Version")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
"@ | Set-Content -LiteralPath $versionFile -Encoding UTF8

try {
    & $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
        "/win32manifest:$manifest" "/resource:$catalog,SkillPeek.skills.tsv" "/out:$staged" `
        /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        (Join-Path $PSScriptRoot "src\App.cs") (Join-Path $PSScriptRoot "src\SkillPanel.cs") $versionFile
    if ($LASTEXITCODE -ne 0) { throw "Compile failed." }

    if (Test-Path -LiteralPath $exe) { [IO.File]::Replace($staged, $exe, [NullString]::Value) }
    else { Move-Item -LiteralPath $staged -Destination $exe }
} finally {
    if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged }
}

# 校验文件，和 Release 一起发布
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLower()
"$hash  SkillPeek.exe" | Set-Content -LiteralPath (Join-Path $dist "SkillPeek.exe.sha256") -Encoding ascii

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

Write-Host "Version: $Version  ($asm)"
Get-ChildItem -LiteralPath $dist | Select-Object Name, Length
