# Extract-ClaudePrompts.ps1
# Reads Claude Code conversation history and extracts all user prompts + token usage.
# Output: HTML report + two CSVs (prompts, token summary) saved locally and copied to shared folder.

param(
    [string]$ClaudeRoot   = "$env:USERPROFILE\.claude",
    [string]$OutputDir    = "$env:USERPROFILE\Desktop",
    [string]$SharedFolder = "\\SHARED\Hack2Hire",
    [string]$LaptopName   = $env:COMPUTERNAME
)

$ErrorActionPreference = "Stop"

# ── helpers ───────────────────────────────────────────────────────────────────

function Get-TextFromContent($content) {
    if ($content -is [string]) { return $content.Trim() }
    if ($content -is [System.Collections.IEnumerable]) {
        $parts = @()
        foreach ($block in $content) {
            if ($block.type -eq "text") {
                $text = $block.text.Trim()
                if ($text -match '<ide_|<system-reminder|<pasted_content|<command-name') { continue }
                if ($text -match '^#{1,3} ' -and $text.Length -gt 300) { continue }
                if ($text -match '(?i)(page contract|read before your first publish|These are the Artifact tool)') { continue }
                if ($text.Length -gt 0) { $parts += $text }
            }
        }
        return ($parts -join " ").Trim()
    }
    return ""
}

function Escape-Html([string]$text) {
    $text -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;' -replace '"','&quot;'
}

function Format-LocalTime([string]$iso) {
    if ([string]::IsNullOrEmpty($iso)) { return "-" }
    try { return ([datetime]$iso).ToLocalTime().ToString("dd MMM yyyy  HH:mm:ss") } catch { return $iso }
}

function Format-Number([long]$n) { $n.ToString("N0") }

# ── extract prompts ───────────────────────────────────────────────────────────

$projectsRoot = Join-Path $ClaudeRoot "projects"
if (-not (Test-Path $projectsRoot)) {
    Write-Warning "Claude projects folder not found: $projectsRoot"
    exit 1
}

$rows           = [System.Collections.Generic.List[PSObject]]::new()
$sessionTokenMap = @{}   # sessionId -> hashtable of token counts

foreach ($projectDir in Get-ChildItem $projectsRoot -Directory) {
    $projectName = $projectDir.Name -replace '^d--Users-[^-]+-', '' -replace '-', '/' -replace '^.*Repos/', ''
    if ($projectName.Length -gt 40) { $projectName = $projectName.Substring($projectName.Length - 40) }

    foreach ($jsonlFile in Get-ChildItem $projectDir.FullName -Filter "*.jsonl") {
        $sessionId    = [System.IO.Path]::GetFileNameWithoutExtension($jsonlFile.Name)
        $sessionShort = $sessionId.Substring(0, [Math]::Min(8, $sessionId.Length))

        if (-not $sessionTokenMap.ContainsKey($sessionId)) {
            $sessionTokenMap[$sessionId] = @{
                Project           = $projectName
                SessionShort      = $sessionShort
                InputTokens       = [long]0
                OutputTokens      = [long]0
                CacheReadTokens   = [long]0
                CacheCreateTokens = [long]0
                FirstTimestamp    = ""
            }
        }

        foreach ($rawLine in [System.IO.File]::ReadAllLines($jsonlFile.FullName)) {
            if ([string]::IsNullOrWhiteSpace($rawLine)) { continue }
            try { $entry = $rawLine | ConvertFrom-Json } catch { continue }

            # Record first timestamp for session ordering
            if (-not [string]::IsNullOrEmpty($entry.timestamp) -and
                [string]::IsNullOrEmpty($sessionTokenMap[$sessionId].FirstTimestamp)) {
                $sessionTokenMap[$sessionId].FirstTimestamp = $entry.timestamp
            }

            # ── user prompts ──────────────────────────────────────────────────
            if ($entry.type -eq "user") {
                $msg = $entry.message
                if ($msg -and $msg.role -eq "user") {
                    $promptText = Get-TextFromContent $msg.content
                    if (-not [string]::IsNullOrWhiteSpace($promptText)) {
                        $rows.Add([PSCustomObject]@{
                            Laptop       = $LaptopName
                            Timestamp    = if ($entry.timestamp) { $entry.timestamp } else { "" }
                            Project      = $projectName
                            SessionId    = $sessionId
                            SessionShort = $sessionShort
                            Prompt       = $promptText
                        })
                    }
                }
            }

            # ── token usage (assistant messages carry a usage block) ───────────
            $usage = $null
            if ($entry.message -and $entry.message.PSObject.Properties.Name -contains 'usage') {
                $usage = $entry.message.usage
            } elseif ($entry.PSObject.Properties.Name -contains 'usage') {
                $usage = $entry.usage
            }

            if ($usage) {
                $st = $sessionTokenMap[$sessionId]
                if ($usage.input_tokens)                { $st.InputTokens       += [long]$usage.input_tokens }
                if ($usage.output_tokens)               { $st.OutputTokens      += [long]$usage.output_tokens }
                if ($usage.cache_read_input_tokens)     { $st.CacheReadTokens   += [long]$usage.cache_read_input_tokens }
                if ($usage.cache_creation_input_tokens) { $st.CacheCreateTokens += [long]$usage.cache_creation_input_tokens }
            }
        }
    }
}

$rows = $rows | Sort-Object Timestamp

# ── token totals ──────────────────────────────────────────────────────────────

$totalInputTokens  = [long]0
$totalOutputTokens = [long]0
$totalCacheRead    = [long]0
foreach ($st in $sessionTokenMap.Values) {
    $totalInputTokens  += $st.InputTokens
    $totalOutputTokens += $st.OutputTokens
    $totalCacheRead    += $st.CacheReadTokens
}
$totalTokens = $totalInputTokens + $totalOutputTokens

# ── build HTML report ─────────────────────────────────────────────────────────

$totalPrompts  = $rows.Count
$totalSessions = ($rows | Select-Object -ExpandProperty SessionId | Sort-Object -Unique).Count
$totalProjects = ($rows | Select-Object -ExpandProperty Project   | Sort-Object -Unique).Count
$firstTime     = if ($rows.Count -gt 0) { Format-LocalTime $rows[0].Timestamp } else { "-" }
$lastTime      = if ($rows.Count -gt 0) { Format-LocalTime $rows[-1].Timestamp } else { "-" }
$generatedAt   = Get-Date -Format "dd MMM yyyy HH:mm:ss"

$totalTokensDisplay  = Format-Number $totalTokens
$totalInputDisplay   = Format-Number $totalInputTokens
$totalOutputDisplay  = Format-Number $totalOutputTokens
$totalCacheDisplay   = Format-Number $totalCacheRead

# Build session colour map (one colour per session)
$palette = @('#4A90D9','#E67E22','#27AE60','#8E44AD','#E74C3C','#16A085','#F39C12','#2980B9','#D35400','#1ABC9C')
$sessionIds = $rows | Select-Object -ExpandProperty SessionId | Sort-Object -Unique
$sessionColour = @{}
$ci = 0
foreach ($sid in $sessionIds) { $sessionColour[$sid] = $palette[$ci % $palette.Count]; $ci++ }
# Colour sessions that had tokens but no user prompts too
foreach ($sid in $sessionTokenMap.Keys) {
    if (-not $sessionColour.ContainsKey($sid)) { $sessionColour[$sid] = $palette[$ci % $palette.Count]; $ci++ }
}

# Build session token summary rows
$sessionTokenRows = ""
$snum = 1
foreach ($sid in ($sessionTokenMap.Keys | Sort-Object { $sessionTokenMap[$_].FirstTimestamp })) {
    $st     = $sessionTokenMap[$sid]
    $colour = $sessionColour[$sid]
    $short  = Escape-Html $st.SessionShort
    $proj   = Escape-Html $st.Project
    $inp    = Format-Number $st.InputTokens
    $out    = Format-Number $st.OutputTokens
    $cr     = Format-Number $st.CacheReadTokens
    $tot    = Format-Number ($st.InputTokens + $st.OutputTokens)
    $sessionTokenRows += @"
    <tr>
      <td class="num">$snum</td>
      <td class="sess"><span class="badge" style="background:$colour">$short</span></td>
      <td class="proj">$proj</td>
      <td class="tok-num">$inp</td>
      <td class="tok-num">$out</td>
      <td class="tok-num">$cr</td>
      <td class="tok-num tok-total">$tot</td>
    </tr>
"@
    $snum++
}

# Build prompt table rows
$tableRows = ""
$rowNum = 1
foreach ($r in $rows) {
    $colour  = $sessionColour[$r.SessionId]
    $time    = Escape-Html (Format-LocalTime $r.Timestamp)
    $project = Escape-Html $r.Project
    $session = Escape-Html $r.SessionShort
    $prompt  = Escape-Html $r.Prompt
    $tableRows += @"
    <tr>
      <td class="num">$rowNum</td>
      <td class="time">$time</td>
      <td class="proj">$project</td>
      <td class="sess"><span class="badge" style="background:$colour">$session</span></td>
      <td class="prompt-cell">$prompt</td>
    </tr>
"@
    $rowNum++
}

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Hack2Hire — AI Prompt Report | $LaptopName</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Segoe UI', Arial, sans-serif; background: #f0f2f5; color: #1a1a2e; }

  header {
    background: linear-gradient(135deg, #1a1a2e 0%, #16213e 60%, #0f3460 100%);
    color: #fff; padding: 32px 40px 24px;
  }
  header h1 { font-size: 26px; font-weight: 700; letter-spacing: 0.5px; }
  header .sub { margin-top: 6px; font-size: 13px; color: #a0b4cc; }

  .section-label {
    padding: 8px 40px 4px;
    font-size: 11px; font-weight: 700; text-transform: uppercase;
    letter-spacing: 1px; color: #888;
  }

  .stats {
    display: flex; gap: 16px; padding: 20px 40px 8px;
    flex-wrap: wrap;
  }
  .card {
    background: #fff; border-radius: 10px; padding: 18px 24px;
    flex: 1; min-width: 130px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.07);
    border-top: 4px solid #0f3460;
  }
  .card .val { font-size: 28px; font-weight: 700; color: #0f3460; }
  .card .lbl { font-size: 11px; color: #666; margin-top: 4px; text-transform: uppercase; letter-spacing: 0.5px; }
  .card.tok  { border-color: #8E44AD; }
  .card.tok .val { color: #8E44AD; font-size: 22px; padding-top: 4px; }

  .search-bar { padding: 16px 40px 8px; }
  .search-bar input {
    width: 100%; max-width: 480px;
    padding: 10px 16px; border: 1px solid #d0d5dd;
    border-radius: 8px; font-size: 14px;
    outline: none; background: #fff;
  }
  .search-bar input:focus { border-color: #0f3460; box-shadow: 0 0 0 3px rgba(15,52,96,0.1); }

  .table-wrap { padding: 0 40px 32px; overflow-x: auto; }
  table { width: 100%; border-collapse: collapse; background: #fff;
          border-radius: 10px; overflow: hidden;
          box-shadow: 0 2px 12px rgba(0,0,0,0.08); }
  thead tr { background: #0f3460; color: #fff; }
  thead tr.tok-head { background: #6C3483; }
  thead th { padding: 12px 16px; text-align: left; font-size: 11px;
             text-transform: uppercase; letter-spacing: 0.6px; font-weight: 600; }
  tbody tr { border-bottom: 1px solid #eef0f4; transition: background 0.15s; }
  tbody tr:last-child { border-bottom: none; }
  tbody tr:hover { background: #f5f8ff; }
  tbody tr.hidden { display: none; }

  td { padding: 11px 16px; font-size: 13px; vertical-align: top; }
  td.num      { color: #999; font-size: 12px; width: 44px; text-align: right; }
  td.time     { color: #555; white-space: nowrap; width: 190px; font-size: 12px; }
  td.proj     { color: #444; font-size: 12px; width: 160px; word-break: break-all; }
  td.sess     { width: 90px; }
  td.tok-num  { text-align: right; font-variant-numeric: tabular-nums; color: #555; width: 110px; }
  td.tok-total{ font-weight: 700; color: #6C3483; }
  td.prompt-cell { line-height: 1.5; color: #1a1a2e; }

  .badge {
    display: inline-block; color: #fff; font-size: 11px;
    padding: 3px 8px; border-radius: 12px; font-family: monospace;
    letter-spacing: 0.3px;
  }

  footer { text-align: center; padding: 20px; font-size: 12px; color: #aaa; }
</style>
</head>
<body>

<header>
  <h1>Hack2Hire &mdash; AI Prompt Report</h1>
  <div class="sub">Laptop: <strong>$LaptopName</strong> &nbsp;&bull;&nbsp; Generated: $generatedAt</div>
</header>

<div class="section-label">Activity</div>
<div class="stats">
  <div class="card"><div class="val">$totalPrompts</div><div class="lbl">Total Prompts</div></div>
  <div class="card"><div class="val">$totalSessions</div><div class="lbl">Sessions</div></div>
  <div class="card"><div class="val">$totalProjects</div><div class="lbl">Projects</div></div>
  <div class="card" style="border-color:#27AE60"><div class="val" style="font-size:15px;padding-top:6px;color:#27AE60">$firstTime</div><div class="lbl">First Prompt</div></div>
  <div class="card" style="border-color:#E67E22"><div class="val" style="font-size:15px;padding-top:6px;color:#E67E22">$lastTime</div><div class="lbl">Last Prompt</div></div>
</div>

<div class="section-label">Token Consumption</div>
<div class="stats">
  <div class="card tok"><div class="val">$totalTokensDisplay</div><div class="lbl">Total Tokens</div></div>
  <div class="card tok"><div class="val">$totalInputDisplay</div><div class="lbl">Input Tokens</div></div>
  <div class="card tok"><div class="val">$totalOutputDisplay</div><div class="lbl">Output Tokens</div></div>
  <div class="card tok"><div class="val">$totalCacheDisplay</div><div class="lbl">Cache-Read Tokens</div></div>
</div>

<div class="section-label" style="padding-top:20px">Token Usage by Session</div>
<div class="table-wrap" style="padding-top:8px">
  <table>
    <thead>
      <tr class="tok-head">
        <th>#</th>
        <th>Session</th>
        <th>Project</th>
        <th style="text-align:right">Input</th>
        <th style="text-align:right">Output</th>
        <th style="text-align:right">Cache Read</th>
        <th style="text-align:right">Total</th>
      </tr>
    </thead>
    <tbody>
$sessionTokenRows
    </tbody>
  </table>
</div>

<div class="section-label" style="padding-top:20px">Prompts</div>
<div class="search-bar">
  <input type="text" id="search" placeholder="Search prompts..." oninput="filterTable(this.value)">
</div>

<div class="table-wrap">
  <table id="promptTable">
    <thead>
      <tr>
        <th>#</th>
        <th>Time</th>
        <th>Project</th>
        <th>Session</th>
        <th>Prompt</th>
      </tr>
    </thead>
    <tbody>
$tableRows
    </tbody>
  </table>
</div>

<footer>Hack2Hire Prompt Extractor &mdash; Claude Code History</footer>

<script>
function filterTable(q) {
  q = q.toLowerCase();
  document.querySelectorAll('#promptTable tbody tr').forEach(function(row) {
    row.classList.toggle('hidden', q && !row.innerText.toLowerCase().includes(q));
  });
}
</script>
</body>
</html>
"@

# ── save files ────────────────────────────────────────────────────────────────

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force $OutputDir | Out-Null }

$dateStamp = (Get-Date -Format "yyyyMMdd_HHmmss")
$htmlFile  = Join-Path $OutputDir "${LaptopName}_report_${dateStamp}.html"
$csvFile   = Join-Path $OutputDir "${LaptopName}_prompts_${dateStamp}.csv"

$tokCsvFile = Join-Path $OutputDir "${LaptopName}_tokens_${dateStamp}.csv"

[System.IO.File]::WriteAllText($htmlFile, $html, [System.Text.Encoding]::UTF8)

$rows | Select-Object Laptop, Timestamp, Project, SessionId, Prompt |
        Export-Csv -Path $csvFile -NoTypeInformation -Encoding UTF8

# Session-level token summary CSV
$sessionTokenMap.GetEnumerator() |
    Sort-Object { $_.Value.FirstTimestamp } |
    ForEach-Object {
        $st = $_.Value
        [PSCustomObject]@{
            Laptop            = $LaptopName
            SessionId         = $_.Key
            Project           = $st.Project
            FirstTimestamp    = $st.FirstTimestamp
            InputTokens       = $st.InputTokens
            OutputTokens      = $st.OutputTokens
            CacheReadTokens   = $st.CacheReadTokens
            CacheCreateTokens = $st.CacheCreateTokens
            TotalTokens       = $st.InputTokens + $st.OutputTokens
        }
    } | Export-Csv -Path $tokCsvFile -NoTypeInformation -Encoding UTF8

Write-Host "HTML report  -> $htmlFile"  -ForegroundColor Green
Write-Host "Prompts CSV  -> $csvFile"   -ForegroundColor Green
Write-Host "Tokens CSV   -> $tokCsvFile" -ForegroundColor Green

# Open the report in the browser automatically
Start-Process $htmlFile

# Copy to shared folder
if (-not [string]::IsNullOrEmpty($SharedFolder)) {
    try {
        if (-not (Test-Path $SharedFolder)) { New-Item -ItemType Directory -Force $SharedFolder | Out-Null }
        Copy-Item $htmlFile    (Join-Path $SharedFolder "${LaptopName}_report_${dateStamp}.html")  -Force
        Copy-Item $csvFile     (Join-Path $SharedFolder "${LaptopName}_prompts_${dateStamp}.csv")  -Force
        Copy-Item $tokCsvFile  (Join-Path $SharedFolder "${LaptopName}_tokens_${dateStamp}.csv")   -Force
        Write-Host "Copied to shared folder -> $SharedFolder" -ForegroundColor Cyan
    } catch {
        Write-Warning "Could not write to shared folder: $_"
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Yellow
Write-Host "  Total prompts  : $totalPrompts"
Write-Host "  Sessions       : $totalSessions"
Write-Host "  Projects       : $totalProjects"
Write-Host ""
Write-Host "=== Token Usage ===" -ForegroundColor Magenta
Write-Host "  Input tokens   : $(Format-Number $totalInputTokens)"
Write-Host "  Output tokens  : $(Format-Number $totalOutputTokens)"
Write-Host "  Cache read     : $(Format-Number $totalCacheRead)"
Write-Host "  TOTAL tokens   : $(Format-Number $totalTokens)"
Write-Host ""
