using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using FTKModFramework.Core.Marketplace;

internal static class FrameworkUpdateChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private static void Reject(Action action, string message) { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } Check(rejected, message); }
    internal static void Run(string fixture)
    {
        List<ReleaseNoteBlock> markdown = ReleaseNotesMarkdown.Parse("# Title\n\nA **bold** and *italic* word with `code_file`.\n\n- First\n  continued\n- Second\n\n1. Ordered\n2. Next");
        Check(markdown[0].Heading == 1 && markdown[0].Text == "Title" && markdown[1].Text.Contains("<b>bold</b>") && markdown[1].Text.Contains("<i>italic</i>"), "release Markdown renders headings and emphasis");
        Check(markdown[2].Text == "• First continued" && markdown[3].Text == "• Second" && markdown[4].Text == "1. Ordered", "release Markdown preserves list boundaries and joins wrapped source lines");
        markdown = ReleaseNotesMarkdown.Parse("| Platform | File |\n| --- | --- |\n| Mac | `a_long_file.zip` |\n| Linux | `b.tar.gz` |");
        Check(markdown.Count == 2 && markdown[0].Text.Contains("<b>Platform:</b> Mac") && markdown[0].Text.Contains("a_long_file.zip"), "release tables become complete labeled records for narrow panes");
        markdown = ReleaseNotesMarkdown.Parse("<size=999>unsafe</size> [Safe](https://example.com/notes) [Bad](file:///tmp/no)\n\n```cs\n<b>literal</b>\n```");
        Check(!markdown[0].Text.Contains("<size=") && markdown[0].Links.Count == 1 && markdown[0].Links[0].Url == "https://example.com/notes" && markdown[1].Code && !markdown[1].Text.Contains("<b>"), "release Markdown neutralizes Unity markup and excludes unsafe link actions");
        Check(!ReleaseNotesMarkdown.SafeUrl("javascript:alert(1)") && !ReleaseNotesMarkdown.SafeUrl("https://user:secret@example.com") && !ReleaseNotesMarkdown.SafeUrl("https://example.com\\evil") && ReleaseNotesMarkdown.SafeUrl("https://example.com/path?q=1"), "release links require unambiguous HTTP or HTTPS destinations");
        markdown = ReleaseNotesMarkdown.Parse("Unclosed **format and [link](broken\n\n~~~\nraw *text*\n~~~");
        Check(markdown[0].Text.Contains("Unclosed **format") && markdown[1].Text == "raw *text*", "malformed Markdown stays readable and fenced code remains literal");
        FrameworkRelease old = new FrameworkRelease { Version = "0.1.0", Tag = "v0.1.0", Available = true };
        FrameworkRelease newer = new FrameworkRelease { Version = "0.1.2", Tag = "v0.1.2", Available = true, Prerelease = true };
        Check(FrameworkUpdatePresentation.EffectiveTarget("preview", old, "0.1.1").StartsWith("Keep v0.1.1"), "automatic preview review does not promise a downgrade");
        Check(!FrameworkUpdatePresentation.IsDowngrade("stable", old, "0.1.1") && FrameworkUpdatePresentation.IsDowngrade("pinned", old, "0.1.1"), "only explicit older pins trigger downgrade guidance");
        Check(FrameworkUpdatePresentation.EffectiveTarget("pinned", old, "0.1.1") == "v0.1.0", "explicit pin review preserves exact older target");
        Check(FrameworkUpdatePresentation.EffectiveTarget("preview", newer, "0.1.1") == "v0.1.2", "automatic review shows a newer available release");
        newer.Available = false;
        Check(FrameworkUpdatePresentation.EffectiveTarget("preview", newer, "0.1.1").StartsWith("Keep v0.1.1"), "unavailable automatic target does not promise installation");
        Check(!FrameworkUpdateRuntime.ValidTag("v0.1.0/../../other") && !FrameworkUpdateRuntime.ValidTag("v01.1.0"), "pin identity rejects traversal and ambiguous version tags");
        string result = Path.Combine(fixture, "framework-update-result.json");
        File.WriteAllText(result, "{\"schemaVersion\":2,\"operationId\":\"expected\"}");
        Reject(delegate { FrameworkUpdateRuntime.ReadResult(result, "expected"); }, "framework update response requires supported schema");
        File.WriteAllText(result, "{\"schemaVersion\":1,\"operationId\":\"other\"}");
        Reject(delegate { FrameworkUpdateRuntime.ReadResult(result, "expected"); }, "framework update response is bound to its request");
        File.WriteAllText(result, "{\"schemaVersion\":1,\"operationId\":\"expected\",\"ok\":true,\"selectedMode\":\"pinned\",\"selectedTag\":\"v0.1.0\",\"releases\":[{\"releaseId\":1,\"tag\":\"v0.1.0\",\"version\":\"0.1.1\"}]}");
        Reject(delegate { FrameworkUpdateRuntime.ReadResult(result, "expected"); }, "release tag/version mismatch is rejected");
        if (OperatingSystem.IsWindows()) return;
        string game = Path.Combine(fixture, "update-game");
        string helperRoot = Path.Combine(game, "BepInEx/ftkmf");
        Directory.CreateDirectory(helperRoot);
        BepInEx.Paths.GameRootPath = game;
        string settings = Path.Combine(helperRoot, "update-settings.json");
        string helper = Path.Combine(helperRoot, "ftkmf-launcher-helper");
        string script = "#!/bin/sh\n" +
            "if [ \"$2\" = select ]; then printf preview > " + ShellQuote(settings) + "; exec sleep 30; fi\n" +
            "op=$(sed -n 's/.*\"operationId\":\"\\([^\"]*\\)\".*/\\1/p' \"$4\")\n" +
            "printf '{\"schemaVersion\":1,\"operationId\":\"%s\",\"ok\":true,\"status\":\"cached\",\"selectedMode\":\"preview\",\"releases\":[],\"launcherCompatible\":true}' \"$op\" > \"$6\"\n";
        File.WriteAllText(helper, script);
        File.SetUnixFileMode(helper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(Path.Combine(helperRoot, "helper.json"), JsonConvert.SerializeObject(new { schemaVersion = 1, protocolVersion = 1, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(helper))).ToLowerInvariant() }));
        Check(FrameworkUpdateRuntime.Start("select", "preview", null, null), "framework preference selection starts through the verified helper");
        Stopwatch watch = Stopwatch.StartNew();
        while (!File.Exists(settings) && watch.ElapsedMilliseconds < 2000) Thread.Sleep(10);
        Check(File.Exists(settings), "fixture commits preference before cancellation");
        FrameworkUpdateRuntime.Cancel();
        watch.Restart();
        while (FrameworkUpdateRuntime.Busy && watch.ElapsedMilliseconds < 2000) { FrameworkUpdateRuntime.Poll(); Thread.Sleep(10); }
        Check(!FrameworkUpdateRuntime.Busy && FrameworkUpdateRuntime.SelectionKnown && FrameworkUpdateRuntime.State.SelectedMode == "preview", "cancel reaps helper and reloads the actual saved preference");
        Check(FrameworkUpdateRuntime.Start("select", "pinned", "v0.1.0", null, 4242), "reviewed pin identity can be sent to the helper");
        bool pinnedIdentitySent = false;
        foreach (string requestPath in Directory.GetFiles(Path.Combine(helperRoot, "update-operations"), "*.request.json"))
        {
            FrameworkUpdateRequest request = JsonConvert.DeserializeObject<FrameworkUpdateRequest>(File.ReadAllText(requestPath));
            if (request.Mode == "pinned" && request.Tag == "v0.1.0" && request.ReleaseId == 4242) pinnedIdentitySent = true;
        }
        Check(pinnedIdentitySent, "pin request carries the exact reviewed release identity");
        FrameworkUpdateRuntime.Cancel();
        watch.Restart();
        while (FrameworkUpdateRuntime.Busy && watch.ElapsedMilliseconds < 2000) { FrameworkUpdateRuntime.Poll(); Thread.Sleep(10); }
        Check(!Directory.Exists(Path.Combine(game, "BepInEx/plugins")), "in-game preference workflow never installs a framework DLL");
    }
    private static string ShellQuote(string value) { return "'" + value.Replace("'", "'\"'\"'") + "'"; }
}
