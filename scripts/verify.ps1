$ErrorActionPreference = "Stop"

dotnet restore DevStatusCenter.slnx --use-lock-file
dotnet build DevStatusCenter.slnx -c Release --no-restore
dotnet test DevStatusCenter.slnx -c Release --no-build --collect:"XPlat Code Coverage"
dotnet publish src/DevStatusCenter.Desktop/DevStatusCenter.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:DebugType=None `
  -o artifacts/win-x64

Write-Host "Validation complete. Build is in artifacts/win-x64." -ForegroundColor Green

