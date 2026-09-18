using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        // TODO (optional): change if you need broader access.
        private static readonly string[] Scopes = { DriveService.Scope.DriveFile };

        // TODO (optional): set to true to skip files that already exist
        // in the target folder (matched by name).
        public static bool SkipExistingFiles { get; set; } = true;

        // ==============================================

        public sealed record UploadResult(int Uploaded, int Skipped, int Failed);

        /// <summary>
        /// Uploads every file in <paramref name="localFolderPath"/> to Drive.
        /// Reports per-file progress via <paramref name="progress"/> (file name being uploaded).
        /// </summary>
        public static async Task<UploadResult> UploadFolderAsync(
            string localFolderPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
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

                    await UploadSingleFileAsync(service, filePath, cancellationToken);
                    uploaded++;
                }
                catch (Exception)
                {
                    failed++;
                    // TODO (optional): log per-file errors here, e.g. Debug.WriteLine(...)
                }
            }

            return new UploadResult(uploaded, skipped, failed);
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
