$ef = "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe"

function Update-Db($context, $startupProject) {
    & $ef database update `
        --project backend.Domain\backend.Domain.csproj `
        --startup-project $startupProject `
        --context $context
}

cd ..\backend

Update-Db "AuthDbContext"     "backend.Auth.Api\backend.Auth.Api.csproj"
Update-Db "TasksDbContext"    "backend.Tasks.Api\backend.Tasks.Api.csproj"
Update-Db "OrdersDbContext"   "backend.Orders.Api\backend.Orders.Api.csproj"
Update-Db "PaymentsDbContext" "backend.Payments.Api\backend.Payments.Api.csproj"
Update-Db "AppDbContext"      "backend.Api\backend.Api.csproj"

echo "All migrations applied successfully."