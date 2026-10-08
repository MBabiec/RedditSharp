using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace RedditSharp
{
    /// <summary>
    /// Syncs the central data.csv (id,name,hash rows) between local disk and
    /// the fixed file on Google Drive. Flow: download on app launch, work on
    /// the local copy (export/upload append to it), override the Drive file
    /// after uploading images.
    /// </summary>
    public static class DriveHashCsvSync
    {
        public const string RemoteFileId = "1Ao1vwb5fme7-PS8d-JSRHEW6jE3tgAd1";

        public static string LocalPath => DriveHashExporter.DefaultOutputPath;

        private static readonly string[] Scopes = { DriveService.Scope.Drive };

        /// <summary>Downloads the Drive data.csv over the local copy.</summary>
        public static async Task DownloadAsync(CancellationToken ct = default)
        {
            using var service = BuildService();
            var request = service.Files.Get(RemoteFileId);
            using var ms = new MemoryStream();
            var status = await request.DownloadAsync(ms, ct);
            if (status == null || status.Status == Google.Apis.Download.DownloadStatus.Failed)
            {
                throw new IOException($"Download of data.csv failed: {status?.Exception?.Message}");
            }
            string? dir = Path.GetDirectoryName(LocalPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllBytesAsync(LocalPath, ms.ToArray(), ct);
            Debug.WriteLine($"data.csv downloaded from Drive ({RemoteFileId})");
        }

        /// <summary>Overrides the Drive data.csv with the local copy.</summary>
        public static async Task UploadAsync(CancellationToken ct = default)
        {
            if (!File.Exists(LocalPath))
            {
                Debug.WriteLine($"No local {LocalPath}, nothing to push to Drive.");
                return;
            }
            using var service = BuildService();
            var metadata = new Google.Apis.Drive.v3.Data.File();
            using var stream = new FileStream(LocalPath, FileMode.Open, FileAccess.Read);
            var request = service.Files.Update(metadata, RemoteFileId, stream, "text/csv");
            request.Fields = "id, modifiedTime";
            var result = await request.UploadAsync(ct);
            if (result.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw new IOException($"Upload of data.csv failed: {result.Exception?.Message}");
            }
            Debug.WriteLine($"data.csv pushed to Drive ({RemoteFileId})");
        }

        private static DriveService BuildService()
        {
            string keyPath = GoogleDriveUploader.ServiceAccountKeyPath;
            if (!File.Exists(keyPath))
            {
                throw new FileNotFoundException($"Service account key not found at '{keyPath}'.");
            }
            GoogleCredential credential;
            using (var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }
            return new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RedditSharp",
            });
        }
    }
}
