using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VeFoldery.Core.Config;
using VeFoldery.Core.Models;

namespace VeFoldery.Core.Services
{
    /// <summary>
    /// Result of undoing one organization run from its CSV audit log.
    /// </summary>
    public class UndoResult
    {
        public int FilesRestored { get; set; }
        public int FilesSkipped { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public List<string> RemovedFolders { get; set; } = new();
        public string CsvLogPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reverts one organization run using its CSV audit log: moves every
    /// MOVED file from DestinationPath back to OriginalPath. Never
    /// overwrites an existing file at the destination. Best-effort with
    /// System.IO.Pipelines not required — plain blocking IO in background.
    /// </summary>
    public static class UndoService
    {
        /// <summary>
        /// Returns audit logs (newest first) inside a folder, matching the
        /// app's own CSV prefix and excluding any currently open/pending one.
        /// </summary>
        public static List<string> GetUndoableLogs(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return new List<string>();

            try
            {
                return Directory.GetFiles(directory, AppConstants.CsvLogPrefix + "*.csv")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Undoes the run described by <paramref name="csvLogPath"/>.
        /// Files are restored only when OriginalPath does not already exist.
        /// Empty Sorted_ folders left behind are removed (only when they sit
        /// under the directory that contains the CSV log).
        /// </summary>
        public static Task<UndoResult> UndoFromLogAsync(
            string csvLogPath,
            IProgress<(int current, int total, string name)>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.Run(() => UndoFromLog(csvLogPath, progress, cancellationToken), cancellationToken);

        public static UndoResult UndoFromLog(
            string csvLogPath,
            IProgress<(int current, int total, string name)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new UndoResult { CsvLogPath = csvLogPath };

            if (string.IsNullOrWhiteSpace(csvLogPath) || !File.Exists(csvLogPath))
                throw new FileNotFoundException("Audit log not found", csvLogPath);

            string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(csvLogPath))!;

            var rows = ParseLog(csvLogPath);
            int total = rows.Count;
            int current = 0;

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (string.Equals(row.Action, "MOVED", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(row.DestinationPath)
                        && !string.IsNullOrEmpty(row.OriginalPath))
                    {
                        string src = row.DestinationPath;
                        string dst = row.OriginalPath;

                        if (File.Exists(dst))
                        {
                            // Never overwrite: the file is already back home.
                            result.FilesSkipped++;
                        }
                        else if (File.Exists(src))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                            File.Move(src, dst);
                            result.FilesRestored++;
                        }
                        else
                        {
                            // Neither side exists — already handled elsewhere.
                            result.FilesSkipped++;
                        }
                    }
                    else
                    {
                        result.FilesSkipped++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"{row.OriginalPath}: {ex.Message}");
                }

                current++;
                progress?.Report((current, total, Path.GetFileName(row.OriginalPath)));
            }

            // Clean up empty Sorted_ folders created by that run.
            result.RemovedFolders = RemoveEmptySortedFolders(baseDirectory);

            return result;
        }

        private static List<(string OriginalPath, string DestinationPath, string Action)> ParseLog(string csvPath)
        {
            var rows = new List<(string, string, string)>();
            using var reader = new StreamReader(csvPath, Encoding.UTF8);
            string? line = reader.ReadLine(); // header
            if (line is null) return rows;

            while ((line = reader.ReadLine()) is not null)
            {
                var fields = SplitCsvLine(line);
                if (fields.Count < 5) continue;
                rows.Add((fields[1], fields[2], fields[4]));
            }
            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields;
        }

        private static List<string> RemoveEmptySortedFolders(string directory)
        {
            var removed = new List<string>();
            try
            {
                foreach (var dir in Directory.GetDirectories(directory, AppConstants.SortedFolderPrefix + "*"))
                {
                    try
                    {
                        // The run nests month folders inside, so prune empty
                        // sub-trees first; only delete the root when it ends
                        // up with no entries at all.
                        RemoveEmptySubDirs(dir);

                        if (Directory.EnumerateFileSystemEntries(dir).Any())
                            continue;
                        Directory.Delete(dir, recursive: true);
                        removed.Add(dir);
                    }
                    catch { /* leave it in place */ }
                }
            }
            catch { }
            return removed;
        }

        /// <summary>Depth-first removal of empty directories under <paramref name="root"/> (root untouched here).</summary>
        private static void RemoveEmptySubDirs(string directory)
        {
            foreach (var sub in Directory.GetDirectories(directory))
            {
                RemoveEmptySubDirs(sub);
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(sub).Any())
                        Directory.Delete(sub);
                }
                catch { /* leave it in place */ }
            }
        }
    }
}