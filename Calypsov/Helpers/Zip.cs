using System;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Calypsov.Helpers;


public static class Zip
{

    public static string ZipFiles(
        List<string> filePaths,
        string outputFolder,
        string zipFileName,
        Action<string>? onFileProcessed = null)
    {
        Directory.CreateDirectory(outputFolder);

        var zipPath = Path.Combine(outputFolder, zipFileName);

        var metadata = new ZipMetadata();

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath))
                    continue;

                var archivePath = Path.GetFileName(filePath);

                // Add actual file
                archive.CreateEntryFromFile(
                    filePath,
                    archivePath);

                // Add metadata
                metadata.Files.Add(new ZipFileMetadata
                {
                    ArchivePath = archivePath,
                    OriginalPath = filePath
                });

                onFileProcessed?.Invoke(filePath);
            }

            // Create manifest.json
            var manifestEntry = archive.CreateEntry("manifest.json");

            using var writer = new StreamWriter(manifestEntry.Open());

            var json = JsonConvert.SerializeObject(
                metadata,
                Formatting.Indented);

            writer.Write(json);
        }

        return zipPath;
    }

    public static string ZipFolders(
        List<string> sourceFolders,
        string outputFolder,
        string zipFileName,
        Action<string>? onFileProcessed = null)
    {
        Directory.CreateDirectory(outputFolder);

        var zipPath = Path.Combine(outputFolder, zipFileName);

        var metadata = new ZipMetadata();

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var sourceFolder in sourceFolders)
            {
                if (!Directory.Exists(sourceFolder))
                    continue;

                var folderName = new DirectoryInfo(sourceFolder).Name;

                var files = Directory.GetFiles(
                    sourceFolder,
                    "*",
                    SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    // Get path relative to the source folder
                    var relativePath = Path.GetRelativePath(
                        sourceFolder,
                        filePath);

                    // Put the folder name at the beginning
                    var archivePath = Path.Combine(
                        folderName,
                        relativePath);

                    // ZIP uses '/' as the path separator
                    archivePath = archivePath.Replace(
                        Path.DirectorySeparatorChar,
                        '/');

                    archive.CreateEntryFromFile(
                        filePath,
                        archivePath);

                    metadata.Files.Add(new ZipFileMetadata
                    {
                        ArchivePath = archivePath,
                        OriginalPath = filePath
                    });

                    onFileProcessed?.Invoke(filePath);
                }
            }

            // Add manifest.json
            var manifestEntry = archive.CreateEntry("manifest.json");

            using var writer = new StreamWriter(
                manifestEntry.Open());

            var json = JsonConvert.SerializeObject(
                metadata,
                Formatting.Indented);

            writer.Write(json);
        }

        return zipPath;
    }

    public static void UnzipFile(string zipPath, Action<string>? onFileProcessed = null)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException(
                "ZIP file not found.",
                zipPath);

        using var archive = ZipFile.OpenRead(zipPath);
        var metadata = ReadManifest(archive);

        // Restore files
        foreach (var file in metadata.Files)
        {
            var entry = archive.GetEntry(file.ArchivePath);

            if (entry == null)
                continue;

            var directory = Path.GetDirectoryName(
                file.OriginalPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            entry.ExtractToFile(
                file.OriginalPath,
                overwrite: true);

            onFileProcessed?.Invoke(file.OriginalPath);
        }
    }

    /// <summary>
    /// Peeks a zip's manifest for its file count without extracting anything — lets a caller
    /// compute an overall progress total up front. Returns 0 if the zip doesn't exist.
    /// </summary>
    public static int CountManifestEntries(string zipPath)
    {
        if (!File.Exists(zipPath))
            return 0;

        using var archive = ZipFile.OpenRead(zipPath);
        return ReadManifest(archive).Files.Count;
    }

    private static ZipMetadata ReadManifest(ZipArchive archive)
    {
        var manifestEntry = archive.GetEntry("manifest.json");

        if (manifestEntry == null)
            throw new InvalidDataException(
                "manifest.json was not found in the ZIP.");

        using var reader = new StreamReader(manifestEntry.Open());
        var json = reader.ReadToEnd();

        var metadata = JsonConvert.DeserializeObject<ZipMetadata>(json);

        if (metadata == null)
            throw new InvalidDataException(
                "Invalid manifest.json.");

        return metadata;
    }
}


public class ZipFileMetadata
{
    public string ArchivePath { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
}

public class ZipMetadata
{
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<ZipFileMetadata> Files { get; set; } = new();
}