[CmdletBinding()]
param(
    [string]$RimWorldDir = 'D:\Appdata\Steam\steamapps\common\RimWorld',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($project in @('Source/LeadYourPet.csproj', 'Guard/Source/LeadYourPetContinuedGuard.csproj')) {
    & dotnet build (Join-Path $root $project) -c $Configuration "-p:RimWorldDir=$RimWorldDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
if ($BuildOnly) { return }
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Exit RimWorld before deploying.' }
$target = Join-Path $RimWorldDir 'Mods/LeadYourPetsContinued'
if (Test-Path $target) {
    if ((Get-Item $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Target is a reparse point.' }
    $about = Join-Path $target 'About/About.xml'
    if (!(Test-Path $about)) { throw 'Existing target has no mod identity.' }
    if (([xml](Get-Content $about -Raw)).ModMetaData.packageId -ne 'nanaloveyuki.leadyourpet.continued') { throw 'Target belongs to another mod.' }
}
$files = @(
    foreach ($folder in @('About','Defs','Languages','Guard/Languages')) {
        Get-ChildItem (Join-Path $root $folder) -File -Recurse
    }
    foreach ($file in @('Assemblies/LeadYourPet.dll','Guard/Assemblies/LeadYourPetContinuedGuard.dll','LoadFolders.xml','NOTICE','README.md','LICENSE')) {
        Get-Item (Join-Path $root $file)
    }
)
foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\','/')
    $destination = Join-Path $target $relative
    New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    if ((Get-FileHash $file.FullName).Hash -ne (Get-FileHash $destination).Hash) { throw "Hash mismatch: $relative" }
}
Write-Host "Deployed and SHA-256 verified $($files.Count) files: $target"
