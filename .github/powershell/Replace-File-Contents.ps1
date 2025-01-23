param(    
    [Parameter(Position=0)]
    [string] 
    $FilePath,
    
    [Parameter(Position=1)]
    [string] 
    $FileContents
)

Write-Host "FilePath: $FilePath"
Write-Host "FileContents: $FileContents"


if (Test-Path $FilePath) { 
  Write-Host "Overwriting file"
}
else {
  Write-Host "Creating file"
}

new-item -ItemType File -Path ($FilePath) -Value $FileContents -Force