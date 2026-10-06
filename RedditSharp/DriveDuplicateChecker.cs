using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;

namespace RedditSharp
{
    /// <summary>
    /// Loads id,name,hash rows from data.csv and finds perceptual-hash
    /// duplicates for a local file. Same algorithm + threshold as
    /// ImageHandler (PerceptualHash, Similarity &gt; 90).
    /// </summary>
    public sealed class DriveDuplicateChecker
    {
        public sealed record DriveEntry(string Id, string Name, ulong Hash);

        public sealed record DuplicateHit(DriveEntry Drive, ulong LocalHash, double Similarity);

        private readonly List<DriveEntry> _entries = new();
        private readonly PerceptualHash _pHash = new();

        public int Count => _entries.Count;

        public double SimilarityThreshold { get; }

        public DriveDuplicateChecker(double similarityThreshold = 90.0)
        {
            SimilarityThreshold = similarityThreshold;
        }

        public void Add(DriveEntry entry) => _entries.Add(entry);

        public static DriveDuplicateChecker? TryLoad(string? csvPath, double similarityThreshold = 90.0)
        {
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                return null;
            }
            var checker = new DriveDuplicateChecker(similarityThreshold);
            foreach (string line in File.ReadLines(csvPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                string[] cols = SplitCsvLine(line);
                if (cols.Length < 3)
                {
                    continue;
                }
                string hashHex = cols[2].Trim();
                if (hashHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    hashHex = hashHex[2..];
                }
                if (!ulong.TryParse(hashHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hash))
                {
                    continue;
                }
                checker.Add(new DriveEntry(cols[0], cols[1], hash));
            }
            return checker;
        }

        /// <summary>Computes the same hash ImageHandler uses. Throws for non-images.</summary>
        public ulong ComputeLocalHash(byte[] bytes)
        {
            using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
            return _pHash.Hash(img);
        }

        public DuplicateHit? FindSimilar(ulong localHash)
        {
            DriveEntry? best = null;
            double bestSim = 0;
            foreach (var entry in _entries)
            {
                double sim = CompareHash.Similarity(localHash, entry.Hash);
                if (sim > SimilarityThreshold && sim > bestSim)
                {
                    bestSim = sim;
                    best = entry;
                }
            }
            return best == null ? null : new DuplicateHit(best, localHash, bestSim);
        }

        internal static string[] SplitCsvLine(string line)
        {
            var cols = new List<string>();
            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            cur.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cur.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    cols.Add(cur.ToString());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            cols.Add(cur.ToString());
            return cols.ToArray();
        }
    }
}
