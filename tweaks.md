# McoreD/ShareX — tweaks vs upstream

- **Fork:** McoreD/ShareX, branch `tweaks` (GitHub default branch; the only branch). Head `02fa80df94dd8b9e9d7dcc1f546b4c3576eb1e85`
- **Upstream:** ShareX/ShareX, branch `develop`. Tip `0899cece0f9f3f98333004a0a2b2091cef42ed3a`
- **Merge base:** `771b49b85dfe07485ca852f563310d85680d5b19` (fork is 54 commits ahead and 1 behind; 11 files differ in the net diff)
- **Generated:** 2026-10-11, from the GitHub compare API (read-only)
- **Note:** This is based on what is on GitHub. Your local checkout at `C:\Users\Public\source\repos\McoreD\ShareX` may have a different branch checked out, or unpushed or uncommitted changes, and those aren't covered here.

## Tweaks

### 1. Branded installer file name
The Inno Setup output file name includes `McoreD`, so it reads `ShareX-<ver>-McoreD-setup-<platform>`.
*Files:* `ShareX.Setup/InnoSetup/ShareX-setup.iss`

### 2. Date-prefixed default file names
The default name patterns start with the date (`%y%mo%d_%ra{10}` and `%y%mo%d_%pn_%ra{10}`) instead of only random characters.
*Files:* `ShareX/TaskSettings.cs`

### 3. "Edit in …" names for external programs, with Paint added only once
The default external programs are renamed "Edit in Paint", "Edit in Paint.NET", "Edit in Adobe Photoshop" and so on, so each action starts with a verb. Paint is only added when the list is first created, so if you delete a built-in app it doesn't come back.
*Files:* `ShareX/TaskHelpers.cs` (`AddDefaultExternalPrograms`)

### 4. FFmpeg "Convert to MP4" and "Convert to GIF" actions
Two external-program actions are added that use the configured FFmpeg. One turns `.mov` into H.264 MP4 (crf 23, faststart), useful for macOS screen recordings. The other turns `.mov` into GIF with palettegen. Both delete the input file afterwards.
*Files:* `ShareX/TaskHelpers.cs`

### 5. Directory Indexer style
The default indexer CSS uses bullet lists (`disc`) and a right-side-only folder border.
*Files:* `ShareX.Tools/Tools/DirectoryIndexer/IndexerDefault.css`

### 6. ShareX.CLI headless capture tool
A new console project with a `capture` command (System.CommandLine). It takes `--outputDir`, `--width`, `--height`, `--seed`, `--x`, `--y` and `--log`, and captures a region using ScreenCaptureLib. A `CompareTest.ps1` script runs ShareX and XerahS captures side by side so the outputs can be compared pixel by pixel. The script has hard-coded `C:\Users\liveu\...` paths.
*Files:* `ShareX.CLI/*`, `ShareX.sln`, `Directory.Packages.props`

### 7. Image editor hook (Avalonia ShareX.Editor), now commented out
Commits from Jan 2026 brought in ShareX.Editor and an Avalonia editor host for annotation. The latest WIP commit (2026-10-10) removes `AvaloniaAppHost`. All that's left is a commented-out block in `AnnotateImageAsync`, so annotation uses the stock editor.
*Files:* `ShareX/TaskHelpers.cs`

### 8. Small housekeeping changes
`.gitignore` ignores `._APIKeysLocal.cs`. `WorkerTask.LoadFileStream` has only a comment added: the "Remove ShowError dialog" change was undone, so behaviour matches upstream.
*Files:* `.gitignore`, `ShareX/WorkerTask.cs`

## No longer in the net diff
These fork commits were later reverted or absorbed by upstream: the watch-folder error dialog removal, `.mov` added as a watch filter, "Open watch folder" (2021), alphabetical reordering, the `autopf` Inno Setup change (#1), and the ShareX.Editor project and platform-target changes.
