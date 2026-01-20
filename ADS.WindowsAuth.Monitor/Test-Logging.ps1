# PowerShell скрипт за тестване на логване от Monitor Service
# Използване: .\Test-Logging.ps1

param(
    [string]$ApiUrl = "https://ads-auth.nursanbulgaria.com",
    [string]$MachineName = $env:COMPUTERNAME
)

Write-Host "=== Тест на логване към API ===" -ForegroundColor Cyan
Write-Host ""

# Тестово логване
$testLog = @{
    MachineName = $MachineName
    Username = $env:USERNAME
    Domain = $env:USERDOMAIN
    Level = "INFO"
    Message = "Тестово логване от PowerShell скрипт - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Timestamp = (Get-Date).ToUniversalTime()
    Source = "TestScript"
}

Write-Host "Изпращане на тестов лог..." -ForegroundColor Yellow
Write-Host "  API URL: $ApiUrl/api/logs/upload" -ForegroundColor Gray
Write-Host "  Machine: $MachineName" -ForegroundColor Gray
Write-Host "  User: $($env:USERNAME)@$($env:USERDOMAIN)" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/api/logs/upload" `
        -Method Post `
        -ContentType "application/json" `
        -Body ($testLog | ConvertTo-Json) `
        -ErrorAction Stop

    Write-Host "✓ Логът е изпратен успешно!" -ForegroundColor Green
    Write-Host "  Response: $($response | ConvertTo-Json)" -ForegroundColor Gray
}
catch {
    Write-Host "✗ Грешка при изпращане на лог:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "  Response: $responseBody" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Проверка на логове в API..." -ForegroundColor Yellow
try {
    $logs = Invoke-RestMethod -Uri "$ApiUrl/api/logs/database/machine/$MachineName" -Method Get
    Write-Host "✓ Намерени $($logs.TotalCount) лога за $MachineName" -ForegroundColor Green
    if ($logs.Logs.Count -gt 0) {
        Write-Host "Последни 5 лога:" -ForegroundColor Cyan
        $logs.Logs | Select-Object -First 5 | ForEach-Object {
            Write-Host "  [$($_.Timestamp)] [$($_.Level)] $($_.Message)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "⚠ Грешка при получаване на логове: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Тестът е завършен ===" -ForegroundColor Cyan

