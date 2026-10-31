$sourceDir = (Resolve-Path "proyecto-fase-2-dotnet\publish-dotnet").Path
$wwwrootDir = Join-Path $sourceDir "wwwroot"
$zipPath = Join-Path (Resolve-Path "proyecto-fase-2-dotnet").Path "itssnp_normativo_dotnet.zip"

# Copy wwwroot files also to root of publish-dotnet for dual-mode static file serving
if (Test-Path $wwwrootDir) {
    Get-ChildItem -Path $wwwrootDir -File | ForEach-Object {
        $dest = Join-Path $sourceDir $_.Name
        if (!(Test-Path $dest)) {
            Copy-Item $_.FullName -Destination $dest -Force
        }
    }
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)

$files = Get-ChildItem -Path $sourceDir -Recurse -File
foreach ($item in $files) {
    $relPath = $item.FullName.Substring($sourceDir.Length + 1).Replace("\", "/")
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $item.FullName, $relPath, [System.IO.Compression.CompressionLevel]::Optimal)
}

$zip.Dispose()

Get-Item $zipPath | Select-Object Name, Length, LastWriteTime
