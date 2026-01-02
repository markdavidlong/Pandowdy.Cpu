# PowerShell script to convert all C# files to UTF-8 without BOM
# Run this from the solution root directory

Write-Host "Converting all .cs files to UTF-8 without BOM..." -ForegroundColor Cyan

# Define directories to process (excluding legacy projects if desired)
$directories = @(
    "Pandowdy.EmuCore",
    "Pandowdy.EmuCore.Tests",
    "Pandowdy.UI",
    "Pandowdy.UI.Tests",
    "Pandowdy"
)

$fileCount = 0
$convertedCount = 0

foreach ($dir in $directories) {
    if (Test-Path $dir) {
        Write-Host "`nProcessing directory: $dir" -ForegroundColor Yellow
        
        Get-ChildItem -Path $dir -Filter "*.cs" -Recurse | ForEach-Object {
            $fileCount++
            $file = $_
            Write-Host "  Checking: $($file.FullName)" -ForegroundColor Gray
            
            try {
                # Read the file content
                $content = Get-Content $file.FullName -Raw
                
                # Check current encoding
                $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
                $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
                
                if ($hasBom -or $content -ne $null) {
                    # Write with UTF-8 encoding without BOM
                    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                    [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
                    $convertedCount++
                    
                    if ($hasBom) {
                        Write-Host "    ✓ Converted (removed BOM)" -ForegroundColor Green
                    } else {
                        Write-Host "    ✓ Converted to UTF-8" -ForegroundColor Green
                    }
                }
            }
            catch {
                Write-Host "    ✗ Error: $_" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "`nDirectory not found: $dir" -ForegroundColor Red
    }
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Conversion complete!" -ForegroundColor Green
Write-Host "Files processed: $fileCount" -ForegroundColor White
Write-Host "Files converted: $convertedCount" -ForegroundColor White
Write-Host "================================================" -ForegroundColor Cyan
