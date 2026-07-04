# Path to Python and script
$PYTHON_EXE = "C:\Python314\python.exe"
$SCRIPT     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generation_script\generation_script.py"

# Define multiple runs as an array of hashtables
$runs = @(
    @{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\micro-generate-canonical-requisition-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-canonical-requisition.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\micro-generate-canonical-requisition.md"
    },
    @{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\standard-generate-canonical-requisition-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-canonical-requisition.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\standard-generate-canonical-requisition.md"
    },
    @{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\complex-generate-canonical-requisition-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-canonical-requisition.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\complex-generate-canonical-requisition.md"
    },
	
# Requisition to solicitation	
	
	@{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\micro-generate-solicitation-from-requisition-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-solicitation-from-requisition.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\micro-generate-solicitation-from-requisition.md"
    }
	@{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\standard-generate-solicitation-from-requisition-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-solicitation-from-requisition.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\standard-generate-solicitation-from-requisition.md"
    },
	@{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\complex-generate-solicitation-from-requisition-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-solicitation-from-requisition.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\complex-generate-solicitation-from-requisition.md"
    },
	
# Solicitation to award	
	
	@{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\micro-generate-award-from-solicitation-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-award-from_solicitation.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\micro-generate-award-from-solicitation.md"
    },
	@{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\standard-generate-award-from-solicitation-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-award-from_solicitation.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\standard-generate-award-from-solicitation.md"
    },
	@{
        DATA_FILE     = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\json_data\complex-generate-award-from-solicitation-parms.json"
        TEMPLATE_FILE = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\parameterized_prompts\generate-award-from_solicitation.md"
        OUTPUT_FILE   = "C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting\generated_prompts\complex-generate-award-from-solicitation.md"
    }
)

# Loop through each run
foreach ($run in $runs) {
    Write-Host "`nRunning generation for:" $run.OUTPUT_FILE -ForegroundColor Cyan

    # Build the input text for the Python prompts
    $inputText = @"
$($run.DATA_FILE)
$($run.TEMPLATE_FILE)
$($run.OUTPUT_FILE)
"@

    # Pipe the answers to Python
    $inputText | & $PYTHON_EXE $SCRIPT

    Write-Host "Completed: $($run.OUTPUT_FILE)" -ForegroundColor Green
}
