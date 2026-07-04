$ErrorActionPreference = 'Stop'

#---------------------------------------------------------------------------------
$cfg = Get-Content "C:\Users\skeye\BOOK2\MAKEDOC\config\makedoc.config.json" | ConvertFrom-Json
$ClausePath = $cfg.ClausesRoot -replace '/', '\'
$DbPath = $cfg.DatabasePath -replace '/', '\'
$SeedPath = $cfg.SeedPath -replace '/', '\'
$PS1Path = $cfg.PS1Path  -replace '/', '\'
$SqlPath = $cfg.SQLPath -replace '/', '\'

Write-Output "Dbpath = " $DbPath
Write-Output "SeedPath = " $SeedPath
Write-Output "PS1Path = " $PS1Path
Write-Output "ClausePath = " $ClausePath

#exit


#---------------------------------------------------------------------------------
Write-Host "Fix DOCX splits for plain-text fill ins: {lineitem} and {SecNo}"
python fix_word_tags.py --dir "$ClausePath" --recursive

#---------------------------------------------------------------------------------
Write-Host "Starting Excel → CSV conversions..."

# Create ONE Excel instance for all conversions
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false

function Convert-XlsxToCsv {
    param(
        [string]$xlsx,
        [string]$csv
    )

    Write-Host "Saving $xlsx to $csv"

    $wb = $excel.Workbooks.Open($xlsx)
    $wb.SaveAs($csv, 6)   # 6 = xlCSV
    $wb.Close($false)

    # Release workbook COM object
    $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($wb)
}

# Convert all three files
Convert-XlsxToCsv "$SeedPath/seed_DocType_table.xlsx"        "$SeedPath/seed_DocType_table.csv"
Convert-XlsxToCsv "$SeedPath/seed_NodeHierarchy_table.xlsx"  "$SeedPath/seed_NodeHierarchy_table.csv"
Convert-XlsxToCsv "$SeedPath/seed_Node_table.xlsx"           "$SeedPath/seed_Node_table.csv"
# Convert-XlsxToCsv "$SeedPath/seed_Instance_table.xlsx"       "$SeedPath/seed_Instance_table.csv"

# Close Excel
$excel.Quit()
$null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
$excel = $null

# Force cleanup of COM wrappers
[GC]::Collect()
[GC]::WaitForPendingFinalizers()

Write-Host "Excel conversions complete."

#---------------------------------------------------------------------------------
Write-Host "Using CSV file:"
Resolve-Path "$SeedPath\seed_Node_table.csv"

#---------------------------------------------------------------------------------
Write-Host "Running SQLite rebuild script..."
Push-Location $SqlPath
try {
    sqlite3.exe $DbPath `
    ".read drop_MAKEDOC_tables.sql" `
    ".read create_MAKEDOC_tables.sql" `
	".read fts_migrate.sql" `
    ".read load_DocType_table.sql" `
    ".read load_NodeHierarchy_table.sql" `
    ".read load_Node_table.sql"

    if ($LASTEXITCODE -ne 0) { throw "SQLite rebuild failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}
Write-Host "SQLite rebuild complete."
#---------------------------------------------------------------------------------
Write-Host "Loading DOCX files into Node table..."

Rscript.exe $PS1Path/UploadNodes.R > $PS1Path/output.uploadNodes.txt
if ($LASTEXITCODE -ne 0) { throw "SQLite rebuild failed with exit code $LASTEXITCODE" }

Push-Location $SqlPath
try {
    sqlite3.exe $DbPath ".read set_empty_Content_to_null.sql"
    if ($LASTEXITCODE -ne 0) { throw "SQLite rebuild failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

Write-Host "UploadNodes.R complete."
#---------------------------------------------------------------------------------
Write-Host "Check nodes to make sure none have empty content ..."
Rscript.exe $PS1Path/CheckNodes.R 
if ($LASTEXITCODE -ne 0) { throw "CheckNodes found nodes with empty content, exit code $LASTEXITCODE" }


Write-Host "CheckNodes.R complete."
#---------------------------------------------------------------------------------
# Write-Host "Load the Template nodes into the Node table..."
# Rscript.exe $PS1Path/AddTemplateNode.R > $PS1Path/output.addTemplateNode.txt
#if ($LASTEXITCODE -ne 0) { throw "SQLite rebuild failed with exit code $LASTEXITCODE" }


# Write-Host "AddTemplateNode.R complete."
#---------------------------------------------------------------------------------
Write-Host "Running NodeHierarchy traversal..."

Rscript.exe $PS1Path/TraverseNodeHierarchy.R > $PS1Path/output.traverse.txt
if ($LASTEXITCODE -ne 0) { throw "SQLite rebuild failed with exit code $LASTEXITCODE" }


Write-Host "TraverseNodeHierarchy.R complete."
#---------------------------------------------------------------------------------
Write-Host "Find fillins in clauses..."
./find_fillins.ps1

#---------------------------------------------------------------------------------
Write-Host "Backfill clause text into Node.PlainText"

python fts_backfill.py --db "$DbPath"
if ($LASTEXITCODE -ne 0) { throw "fts_backfill.py failed with exit code $LASTEXITCODE" }

#---------------------------------------------------------------------------------
Write-Host "Database rebuild pipeline finished successfully."
