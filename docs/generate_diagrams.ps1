# Generate AppleSAWS Class Hierarchy Diagrams
# This script generates PNG, SVG, and PDF versions of all class hierarchy diagrams

Write-Host "AppleSAWS Class Hierarchy Diagram Generator" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Graphviz is installed
try {
    $null = Get-Command dot -ErrorAction Stop
    Write-Host "[OK] Graphviz is installed" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Graphviz is not installed or not in PATH" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Graphviz:" -ForegroundColor Yellow
    Write-Host "  - Windows: choco install graphviz  OR  scoop install graphviz" -ForegroundColor Yellow
    Write-Host "  - Or download from: https://graphviz.org/download/" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host ""

# Define formats and files
$formats = @('png', 'svg', 'pdf')
$files = @(
    @{
        Name = 'AppleSAWS_ClassHierarchy'
        Description = 'Complete class hierarchy'
    },
    @{
        Name = 'AppleSAWS_ClassHierarchy_Simple'
        Description = 'Simplified class hierarchy'
    },
    @{
        Name = 'AppleSAWS_FileViewer_Architecture'
        Description = 'File and viewer architecture'
    }
)

$successCount = 0
$failCount = 0

# Generate diagrams
foreach ($file in $files) {
    Write-Host "Processing: $($file.Description)" -ForegroundColor Cyan
    
    foreach ($format in $formats) {
        $inputFile = "$($file.Name).dot"
        $outputFile = "$($file.Name).$format"
        
        if (-not (Test-Path $inputFile)) {
            Write-Host "  [SKIP] $inputFile not found" -ForegroundColor Yellow
            $failCount++
            continue
        }
        
        try {
            Write-Host "  Generating $outputFile..." -NoNewline
            $cmd = "dot"
            $args = @("-T$format", $inputFile, "-o", $outputFile)
            $result = & $cmd $args 2>&1
            
            if ($LASTEXITCODE -eq 0 -and (Test-Path $outputFile)) {
                Write-Host " [OK]" -ForegroundColor Green
                $successCount++
            } else {
                Write-Host " [FAILED]" -ForegroundColor Red
                if ($result) {
                    Write-Host "    Error: $result" -ForegroundColor Red
                }
                $failCount++
            }
        } catch {
            Write-Host " [FAILED]" -ForegroundColor Red
            Write-Host "    Error: $_" -ForegroundColor Red
            $failCount++
        }
    }
    
    Write-Host ""
}

# Summary
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Generation Complete!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Success: $successCount files" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "Failed:  $failCount files" -ForegroundColor Red
}
Write-Host ""

# List generated files
if ($successCount -gt 0) {
    Write-Host "Generated files:" -ForegroundColor Cyan
    Get-ChildItem -Path . -Include "AppleSAWS_*.png", "AppleSAWS_*.svg", "AppleSAWS_*.pdf" -File | 
        Sort-Object Name |
        ForEach-Object {
            $size = [math]::Round($_.Length / 1KB, 2)
            Write-Host "  $($_.Name) ($size KB)" -ForegroundColor Gray
        }
}
