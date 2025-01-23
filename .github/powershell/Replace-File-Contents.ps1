param(    
    [Parameter(Position=0)]
    [string] 
    $FilePath,
    
    [Parameter(Position=1)]
    [string] 
    $NewContents
)


if (Test-Path $FilePath) { 
  Write-Host "Overwriting file"
}
else {
  Write-Host "Creating file"
}

new-item -ItemType File -Path ($FilePath) -Value $NewContents -Force