$ErrorActionPreference = "Stop"

git config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "Git hooks enabled: .githooks"
