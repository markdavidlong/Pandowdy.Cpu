# PowerShell script to convert all Markdown files to UTF-8 without BOM
# Run this from the solution root directory

Write-Host "Converting all .md files to UTF-8 without BOM..." -ForegroundColor Cyan

# Search entire repository (excluding legacy if desired)
$excludeDirs = @('.git', 'bin', 'obj', 'node_modules', '.vs')

$fileCount = 0
$convertedCount = 0
$skippedCount = 0

Write-Host "`nSearching for .md files..." -ForegroundColor Yellow

Get-ChildItem -Path . -Filter "*.md" -Recurse | Where-Object {
    $path = $_.FullName
    $exclude = $false
    foreach ($dir in $excludeDirs) {
        if ($path -match [regex]::Escape("\$dir\")) {
            $exclude = $true
            break
        }
    }
    -not $exclude
} | ForEach-Object {
    $fileCount++
    $file = $_
    Write-Host "  Checking: $($file.FullName)" -ForegroundColor Gray
    
    try {
        # Read the file content
        $content = Get-Content $file.FullName -Raw
        
        # Check current encoding
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
        
        if ($content -ne $null) {
            # Write with UTF-8 encoding without BOM
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
            $convertedCount++
            
            if ($hasBom) {
                Write-Host "    ✓ Converted (removed BOM)" -ForegroundColor Green
            } else {
                Write-Host "    ✓ Converted to UTF-8" -ForegroundColor Green
            }
        } else {
            Write-Host "    - Skipped (empty file)" -ForegroundColor Yellow
            $skippedCount++
        }
    }
    catch {
        Write-Host "    ✗ Error: $_" -ForegroundColor Red
    }
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Conversion complete!" -ForegroundColor Green
Write-Host "Files found: $fileCount" -ForegroundColor White
Write-Host "Files converted: $convertedCount" -ForegroundColor White
Write-Host "Files skipped: $skippedCount" -ForegroundColor White
Write-Host "================================================" -ForegroundColor Cyan
