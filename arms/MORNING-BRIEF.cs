#!/usr/bin/env dotnet-script
// SI:
// ARM: MORNING-BRIEF
// INTENT: Produce a structured morning brief from the local reef
// SOURCES: git.commits, reef.trace
// OUTPUT: JSON — { commits_today, trace_summary, generated_at }
// :SI

using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

// Read args from stdin (passed by run_arm as JSON)
var stdinRaw = Console.In.ReadToEnd();
var argsDoc = stdinRaw.Length > 0
    ? JsonDocument.Parse(stdinRaw)
    : JsonDocument.Parse("{}");

// Priority: stdin args → env var → default
var repoPath = (argsDoc.RootElement.TryGetProperty("repo_path", out var rp) ? rp.GetString() : null)
    ?? Environment.GetEnvironmentVariable("REACH_REPO")
    ?? Environment.GetEnvironmentVariable("WORKSPACE") + "/reach"
    ?? ".";

var tracePath = (argsDoc.RootElement.TryGetProperty("trace_file", out var tf) ? tf.GetString() : null)
    ?? Environment.GetEnvironmentVariable("TRACE_FILE")
    ?? "./reach-trace.ndjson";

// --- Git: commits today ---
var commitsToday = new List<string>();
try
{
    var proc = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{repoPath}\" log --oneline --since=midnight --all",
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }
    };
    proc.Start();
    var output = proc.StandardOutput.ReadToEnd();
    proc.WaitForExit();
    foreach (var line in output.Split('\n'))
        if (!string.IsNullOrWhiteSpace(line))
            commitsToday.Add(line.Trim());
}
catch { /* git not available or not a repo — skip */ }

// --- TRACE: count events since midnight ---
var today = DateTime.UtcNow.Date;
var eventCount = 0;
var eventTypes = new Dictionary<string, int>();

if (File.Exists(tracePath))
{
    foreach (var line in File.ReadLines(tracePath))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("timestamp", out var ts)) continue;
            if (!DateTime.TryParse(ts.GetString(), out var dt)) continue;
            if (dt.ToUniversalTime().Date < today) continue;

            eventCount++;
            if (doc.RootElement.TryGetProperty("event_type", out var et))
            {
                var key = et.GetString() ?? "UNKNOWN";
                eventTypes[key] = eventTypes.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }
        catch { }
    }
}

// --- Output ---
var result = new
{
    generated_at = DateTime.UtcNow.ToString("O"),
    commits_today = commitsToday,
    trace_today = new
    {
        event_count = eventCount,
        by_type = eventTypes,
    }
};

Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
