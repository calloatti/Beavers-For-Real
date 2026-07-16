# Define the source folder containing the .cs files
$sourceDir = "C:\Users\calloatti\source\repos\Mods\SimpleConfig\SimpleConfig"

# Get the current directory where the script is being executed
$currentDir = Get-Location

# Fetch all .cs files from the source directory
$csFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -File

# Loop through each file and create/overwrite a hard link
foreach ($file in $csFiles) {
    $linkPath = Join-Path -Path $currentDir -ChildPath $file.Name
    
    # The -Force flag ensures any existing file is overwritten by the new hard link
    New-Item -ItemType HardLink -Path $linkPath -Target $file.FullName -Force | Out-Null
    Write-Host "Created/Overwrote hard link for '$($file.Name)'" -ForegroundColor Green
}