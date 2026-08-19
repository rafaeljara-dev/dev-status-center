param(
  [string]$ProcessName = "DevStatusCenter",
  [int]$SampleSeconds = 60
)

$ErrorActionPreference = "Stop"
$process = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$cpuBefore = $process.TotalProcessorTime.TotalMilliseconds
$started = Get-Date

Start-Sleep -Seconds $SampleSeconds

$process.Refresh()
$elapsedMilliseconds = ((Get-Date) - $started).TotalMilliseconds
$logicalProcessors = [Environment]::ProcessorCount
$cpuPercent = (($process.TotalProcessorTime.TotalMilliseconds - $cpuBefore) / $elapsedMilliseconds / $logicalProcessors) * 100

[PSCustomObject]@{
  Process = $process.ProcessName
  SampleSeconds = $SampleSeconds
  CpuPercentAverage = [math]::Round($cpuPercent, 4)
  WorkingSetMB = [math]::Round($process.WorkingSet64 / 1MB, 2)
  PrivateMemoryMB = [math]::Round($process.PrivateMemorySize64 / 1MB, 2)
  Threads = $process.Threads.Count
}
