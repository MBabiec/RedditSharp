using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace RedditSharp
{
    /// <summary>
    /// Shell for uploading files to Google Drive via a service account.
    /// Fill in the TODOs below, then call <see cref="UploadFolderAsync"/>
    /// (already wired to the "Upload to Drive" button in MainWindow).
    /// </summary>
    public static class GoogleDriveUploader
    {
        // ============ TODO: FILL THESE IN ============

        // TODO: Path to your service-account JSON key file.
        // e.g. @"C:\secrets\my-project-123456.json" or a path relative to the exe.
        public static string ServiceAccountKeyPath { get; set; } = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "serviceAccountCredentials.json");

        // TODO: ID of the Drive folder to upload into.
        // Open the folder in drive.google.com and copy the last part of the URL:
        // https://drive.google.com/drive/folders/THIS_PART_IS_THE_ID
        // NOTE: share that folder with your service-account email
        // (service-account-email@project.iam.gserviceaccount.com) as Editor.
        // Leave empty/null to upload to the service account's My Drive root.
        public static string? DriveFolderId { get; set; } = "1ltfMIxbQEm-LmLR7d-OUcXyEBxaGDn5u";

        // Full Drive scope: uploads (like before) plus listing/downloading
        // arbitrary Drive images for the duplicate preview popup.
        // (DriveFile alone can't read files created outside this app.)
        private static readonly string[] Scopes = { DriveService.Scope.Drive };

        // TODO (optional): set to true to skip files that already exist
        // in the target folder (matched by name).
        public static bool SkipExistingFiles { get; set; } = true;

        // ==============================================

        public sealed record UploadResult(int Uploaded, int Skipped, int Failed);

        /// <summary>
        /// Uploads every file in <paramref name="localFolderPath"/> to Drive.
        /// Reports per-file progress via <paramref name="progress"/> (file name being uploaded).
        /// Before each upload, the file's PerceptualHash (same as ImageHandler)
        /// is compared against <paramref name="hashCsvPath"/> (id,name,hash rows,
        /// defaults to data.csv). On a match above <paramref name="similarityThreshold"/>
        /// a popup shows the Drive image vs the new one; the user picks upload or skip.
        /// The Drive file is never modified. If the CSV is missing, uploads proceed unchecked.
        /// </summary>
        public static async Task<UploadResult> UploadFolderAsync(
            string localFolderPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default,
            string? hashCsvPath = null,
            double similarityThreshold = 90.0)
        {
            if (!Directory.Exists(localFolderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {localFolderPath}");
            }

            if (!File.Exists(ServiceAccountKeyPath))
            {
                throw new FileNotFoundException(
                    $"Service account key not found at '{ServiceAccountKeyPath}'. " +
                    "Set GoogleDriveUploader.ServiceAccountKeyPath to your JSON key file.");
            }

            string[] files = Directory.GetFiles(localFolderPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);
            if (files.Length == 0)
            {
                return new UploadResult(0, 0, 0);
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

            int uploaded = 0, skipped = 0, failed = 0;

            string csvPath = hashCsvPath ?? DriveHashExporter.DefaultOutputPath;
            DriveDuplicateChecker? checker = DriveDuplicateChecker.TryLoad(csvPath, similarityThreshold);

            foreach (string filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(filePath);
                progress?.Report(fileName);

                try
                {
                    if (SkipExistingFiles && !string.IsNullOrWhiteSpace(DriveFolderId))
                    {
                        if (await FileExistsAsync(service, fileName, cancellationToken))
                        {
                            skipped++;
                            continue;
                        }
                    }

                    // Perceptual-hash duplicate check against data.csv.
                    ulong localHash = 0;
                    bool hashComputed = false;
                    if (checker != null)
                    {
                        var hit = TryFindDuplicate(filePath, checker, out localHash, out hashComputed);
                        if (hit != null)
                        {
                            bool uploadAnyway = await AskUploadAnywayAsync(
                                service, filePath, fileName, localHash, hit, cancellationToken);
                            if (!uploadAnyway)
                            {
                                skipped++;
                                continue;
                            }
                        }
                    }

                    string? newId = await UploadSingleFileAsync(service, filePath, cancellationToken);
                    uploaded++;

                    // Remember the new hash so later files in this run (and
                    // future runs) match against it. Never touches Drive content.
                    if (checker != null && hashComputed && !string.IsNullOrEmpty(newId))
                    {
                        var entry = new DriveDuplicateChecker.DriveEntry(
                            newId, fileName, localHash);
                        checker.Add(entry);
                        try
                        {
                            await File.AppendAllTextAsync(
                                csvPath,
                                $"{EscapeCsv(newId)},{EscapeCsv(fileName)},{localHash:x16}{Environment.NewLine}",
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not append to {csvPath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception)
                {
                    failed++;
                    // TODO (optional): log per-file errors here, e.g. Debug.WriteLine(...)
                }
            }

            return new UploadResult(uploaded, skipped, failed);
        }

        /// <summary>
        /// Returns the duplicate hit for a local file, or null when there is
        /// none (or the file isn't a decodable image – then it uploads unchecked).
        /// </summary>
        private static DriveDuplicateChecker.DuplicateHit? TryFindDuplicate(
            string filePath, DriveDuplicateChecker checker,
            out ulong localHash, out bool hashComputed)
        {
            localHash = 0;
            hashComputed = false;
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                localHash = checker.ComputeLocalHash(bytes);
                hashComputed = true;
                return checker.FindSimilar(localHash);
            }
            catch (Exception ex)
            {
                // Non-image / corrupt file: don't block the upload.
                System.Diagnostics.Debug.WriteLine($"Hash check skipped for {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads the Drive match and shows the side-by-side popup.
        /// Returns true = upload anyway, false = skip. The Drive file is
        /// never modified. Fail-open: if the Drive preview can't load,
        /// the upload proceeds.
        /// </summary>
        private static async Task<bool> AskUploadAnywayAsync(
            DriveService service,
            string localPath, string localName, ulong localHash,
            DriveDuplicateChecker.DuplicateHit hit,
            CancellationToken ct)
        {
            byte[] localBytes;
            byte[] driveBytes;
            try
            {
                localBytes = await File.ReadAllBytesAsync(localPath, ct);
                driveBytes = await DownloadBytesAsync(service, hit.Drive.Id, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Duplicate preview unavailable, uploading: {ex.Message}");
                return true;
            }

            bool? decision = null;
            void show()
            {
                var dlg = new DuplicateReviewWindow(
                    localName, localBytes,
                    hit.Drive.Name, driveBytes,
                    hit.Similarity)
                {
                    Owner = Application.Current?.MainWindow
                };
                decision = dlg.ShowDialog();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(show);
            }
            else
            {
                show();
            }

            // Closed via X (null) counts as Skip – the safe default.
            return decision == true;
        }

        private static async Task<byte[]> DownloadBytesAsync(
            DriveService service, string fileId, CancellationToken ct)
        {
            var request = service.Files.Get(fileId);
            using var ms = new MemoryStream();
            var status = await request.DownloadAsync(ms, ct);
            if (status == null || status.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                throw new IOException($"Download failed for {fileId}: {status?.Exception?.Message}");
            }
            return ms.ToArray();
        }

        private static string EscapeCsv(string? value)
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

        private static async Task<bool> FileExistsAsync(
            DriveService service, string fileName, CancellationToken ct)
        {
            // Escape single quotes for the Drive query language.
            string escaped = fileName.Replace("'", "\\'");
            var request = service.Files.List();
            request.Q = $"name = '{escaped}' and '{DriveFolderId}' in parents and trashed = false";
            request.Fields = "files(id, name)";
            request.PageSize = 1;
            var result = await request.ExecuteAsync(ct);
            return result.Files != null && result.Files.Count > 0;
        }

        private static async Task<string?> UploadSingleFileAsync(
            DriveService service, string filePath, CancellationToken ct)
        {
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = Path.GetFileName(filePath),
                Parents = string.IsNullOrWhiteSpace(DriveFolderId)
                    ? null
                    : new List<string> { DriveFolderId },
            };

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var request = service.Files.Create(metadata, stream, GetMimeType(filePath));
            request.Fields = "id, name";
            await request.UploadAsync(ct);
            return request.ResponseBody?.Id;
        }

        private static string GetMimeType(string filePath)
        {
            // TODO (optional): extend this map if you save non-image files.
            return Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream",
            };
        }
    }
}
