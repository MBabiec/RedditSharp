using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CoenM.ImageHash.HashAlgorithms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace RedditSharp
{
    /// <summary>
    /// Mirrors the Python DriveDehash app (driver.py + gatherHashes.py) but
    /// computes the hash exactly like <see cref="ImageHandler"/>:
    /// CoenM <see cref="PerceptualHash"/> over
    /// SixLabors.ImageSharp.Image.Load{Rgba32}(bytes).
    /// Output CSV has no header and matches data/data.csv layout:
    /// id,name,hash  where hash is 16-char lowercase hex (ulong x16).
    /// Re-runs resume via existing IDs, and rows whose ID no longer exists
    /// on Drive (deleted/trashed/unshared) are pruned from the CSV.
    /// </summary>
    public static class DriveHashExporter
    {
        public sealed record ExportResult(int Hashed, int Skipped, int Failed, int Pruned);

        private static readonly string[] Scopes = { DriveService.Scope.DriveReadonly };

        public static string DefaultOutputPath =>
            Path.Combine(Directory.GetCurrentDirectory(), "data.csv");

        public static string ServiceAccountKeyPath =>
            GoogleDriveUploader.ServiceAccountKeyPath;

        public static async Task<ExportResult> ExportAsync(
            string? outputCsvPath = null,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string csvPath = outputCsvPath ?? DefaultOutputPath;

            if (!File.Exists(ServiceAccountKeyPath))
            {
                throw new FileNotFoundException(
                    $"Service account key not found at '{ServiceAccountKeyPath}'.");
            }

            var existingRows = LoadRows(csvPath);
            var alreadyDone = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, _) in existingRows)
            {
                alreadyDone.Add(id);
            }

            GoogleCredential credential;
            using (var stream = new FileStream(ServiceAccountKeyPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }

            using var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RedditSharp",
            });

            var pHash = new PerceptualHash();
            int hashed = 0, skipped = 0, failed = 0;

            // Phase 1: list everything on Drive (same query as before).
            var driveFiles = new List<Google.Apis.Drive.v3.Data.File>();
            var driveIds = new HashSet<string>(StringComparer.Ordinal);
            string? pageToken = null;
            do
            {
                var list = service.Files.List();
                list.PageSize = 100;
                // Same fields as Python driver.get_next_batch.
                list.Fields = "nextPageToken, files(id, name, fileExtension, mimeType)";
                list.PageToken = pageToken;
                // Don't pick up trash; Python lists everything, but skipping
                // trashed files is strictly safer and never hides live images.
                list.Q = "trashed = false";

                var result = await list.ExecuteAsync(cancellationToken);
                if (result.Files != null)
                {
                    foreach (var f in result.Files)
                    {
                        driveFiles.Add(f);
                        if (!string.IsNullOrEmpty(f.Id))
                        {
                            driveIds.Add(f.Id);
                        }
                    }
                }
                pageToken = result.NextPageToken;
            } while (!string.IsNullOrEmpty(pageToken));

            // Phase 2: prune CSV rows whose image is gone from Drive.
            var keptLines = new List<string>(existingRows.Count);
            foreach (var (id, line) in existingRows)
            {
                if (driveIds.Contains(id))
                {
                    keptLines.Add(line);
                }
            }
            int pruned = existingRows.Count - keptLines.Count;

            // Phase 3: hash new Drive images (resume via alreadyDone).
            var newLines = new List<string>();
            foreach (var file in driveFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool isImage = file.MimeType != null &&
                    file.MimeType.Contains("image", StringComparison.OrdinalIgnoreCase);
                if (!isImage || alreadyDone.Contains(file.Id))
                {
                    skipped++;
                    continue;
                }

                progress?.Report(file.Name ?? file.Id);
                try
                {
                    byte[] bytes = await DownloadBytesAsync(service, file.Id, cancellationToken);
                    using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                    ulong hash = pHash.Hash(img); // identical to ImageHandler
                    string hex = hash.ToString("x16");
                    newLines.Add($"{Escape(file.Id)},{Escape(file.Name)},{hex}");
                    alreadyDone.Add(file.Id);
                    hashed++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hash failed for {file.Name} ({file.Id}): {ex.Message}");
                    failed++;
                }
            }

            // Phase 4: rewrite CSV only if something changed (prunes or additions).
            if (pruned > 0 || newLines.Count > 0)
            {
                string? dir = Path.GetDirectoryName(csvPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                keptLines.AddRange(newLines);
                await File.WriteAllLinesAsync(csvPath, keptLines, cancellationToken);
            }

            return new ExportResult(hashed, skipped, failed, pruned);
        }

        private static async Task<byte[]> DownloadBytesAsync(
            DriveService service, string fileId, CancellationToken ct)
        {
            var request = service.Files.Get(fileId);
            using var ms = new MemoryStream();
            // Files.Get without explicit Alt still downloads media via Download().
            var status = await request.DownloadAsync(ms, ct);
            if (status == null || status.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                throw new IOException($"Download failed for {fileId}: {status?.Exception?.Message}");
            }
            return ms.ToArray();
        }

        private static List<(string Id, string Line)> LoadRows(string csvPath)
        {
            var rows = new List<(string, string)>();
            if (!File.Exists(csvPath))
            {
                return rows;
            }
            foreach (string line in File.ReadLines(csvPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                // ID is the first column; it never contains commas/quotes.
                int comma = line.IndexOf(',');
                string id = comma < 0 ? line.Trim() : line[..comma].Trim().Trim('"');
                if (!string.IsNullOrEmpty(id))
                {
                    rows.Add((id, line));
                }
            }
            return rows;
        }

        private static string Escape(string? value)
        {
            value ??= string.Empty;
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value}\"";
            }
            return value;
        }
    }
}
