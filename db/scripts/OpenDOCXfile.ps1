param(
    [Parameter(Mandatory=$true)]
    [string]$RootDir,

    [Parameter(Mandatory=$true)]
    [string]$FileName   # e.g. "NL-0163.docx"
)

# Recursively search for the file
$match = Get-ChildItem -Path $RootDir -Filter $FileName -Recurse -File -ErrorAction SilentlyContinue

if ($match) {
    Write-Host "Opening: $($match.FullName)"
    Invoke-Item $match.FullName
} else {
    Write-Host "File not found under $RootDir"
}

#How to use: .\OpenDOCXfile.ps1 -RootDir "C:\HEAD" -FileName "NL-0163.docx"