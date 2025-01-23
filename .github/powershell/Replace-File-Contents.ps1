param(    
    [Parameter(Position=0)]
    [string] 
    $FolderPath,

    [Parameter(Position=1)]
    [string] 
    $FileName,

    [Parameter(Position=2)]
    [string] 
    $FileContents
)

$FilePath = "$FolderPath\$FileName"

Write-Host "FilePath: $FilePath"

new-item -ItemType File -Path ($FilePath) -Value $FileContents -Force