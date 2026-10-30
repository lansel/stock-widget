$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path bin | Out-Null

$selftestSrc = @("selftest\SelfTest.cs")
foreach ($f in @("QuoteParser.cs","StockList.cs","TradingClock.cs","QuoteClient.cs","PosConfig.cs")) {
  $p = "src\$f"
  if (Test-Path $p) { $selftestSrc += $p }
}
& $csc -nologo -codepage:65001 -out:bin\SelfTest.exe -r:System.Net.Http.dll -r:System.Core.dll -r:System.Drawing.dll @selftestSrc
if ($LASTEXITCODE -ne 0) { exit 1 }
& bin\SelfTest.exe
if ($LASTEXITCODE -ne 0) { Write-Host "SELFTEST FAILED"; exit 1 }

$mainSrc = @(Get-ChildItem src\*.cs | ForEach-Object { $_.FullName })
& $csc -nologo -codepage:65001 -out:bin\StockWidget.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Net.Http.dll -r:System.Core.dll $mainSrc
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "BUILD OK"
