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
    /// </summary>
    public static class DriveHashExporter
    {
        public sealed record ExportResult(int Hashed, int Skipped, int Failed);

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

            var alreadyDone = LoadDoneIds(csvPath);

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

            // Append mode so a re-run resumes (same as Python gatherHashes.py).
            using var writer = new StreamWriter(csvPath, append: true);

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
                var files = result.Files;
                if (files == null || files.Count == 0)
                {
                    pageToken = result.NextPageToken;
                    continue;
                }

                foreach (var file in files)
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
                        writer.WriteLine($"{Escape(file.Id)},{Escape(file.Name)},{hex}");
                        await writer.FlushAsync();
                        alreadyDone.Add(file.Id);
                        hashed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Hash failed for {file.Name} ({file.Id}): {ex.Message}");
                        failed++;
                    }
                }

                pageToken = result.NextPageToken;
            } while (!string.IsNullOrEmpty(pageToken));

            return new ExportResult(hashed, skipped, failed);
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

        private static HashSet<string> LoadDoneIds(string csvPath)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(csvPath))
            {
                return set;
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
                    set.Add(id);
                }
            }
            return set;
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
