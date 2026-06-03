$ef = "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe"

Set-Location $PSScriptRoot\..\backend\backend.Domain

$contexts = @(
    # @{ Name = "AppDbContext";     Db = "vue_demo" },
    @{ Name = "AuthDbContext";     Db = "vue_demo_auth";	 Reportspath = "Migrations/AuthDb"},
    @{ Name = "TasksDbContext";    Db = "vue_demo_tasks"; 	 Reportspath = "Migrations/TasksDb"},
    @{ Name = "OrdersDbContext";   Db = "vue_demo_orders"; 	 Reportspath = "Migrations/OrdersDb"},
    @{ Name = "PaymentsDbContext"; Db = "vue_demo_payments"; Reportspath =  "Migrations/PaymentsDb"}
)

foreach ($ctx in $contexts) {
    Write-Host "`n--- Migrate $($ctx.Db) ($($ctx.Name)) ---" -ForegroundColor Cyan
    & $ef database update --context $ctx.Name -- --reportspath $ctx.Reportspath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($ctx.Name) ($($ctx.Db))" -ForegroundColor Red
    } else {
        Write-Host "OK: $($ctx.Name) ($($ctx.Db))" -ForegroundColor Green
    }
}

echo "`nAll migrations applied."
