using FileOrganizer.Models;
using FileOrganizer.Services;
using Xunit;

namespace TimeFold.Core.Tests
{
    public class LogicTests
    {
        [Theory]
        [InlineData(".json", "JSON Files")]
        [InlineData(".xml", "Data & Config Files")]
        [InlineData(".env", "Data & Config Files")]
        [InlineData(".blend", "3D Files")]
        [InlineData(".gltf", "3D Files")]
        [InlineData(".appimage", "App Installers")]
        [InlineData(".deb", "App Installers")]
        [InlineData(".dmg", "App Installers")]
        [InlineData(".apk", "App Installers")]
        [InlineData(".exe", "App Installers")]
        [InlineData(".psd", "Photoshop Files")]
        [InlineData(".svg", "Vector Files")]
        [InlineData(".cs", "Code Files")]
        [InlineData(".mp4", "Video Files")]
        [InlineData(".mp3", "Audio Files")]
        [InlineData(".zip", "Zip & Archives")]
        [InlineData(".ttf", "Font Files")]
        [InlineData(".docx", "Office Files")]
        [InlineData(".pdf", "PDF Files")]
        [InlineData(".epub", "Reader Files")]
        [InlineData(".txt", "Text & Notes")]
        [InlineData(".sh", "Script Files")]
        public void GetCategory_MapsKnownExtensions(string ext, string expected)
        {
            Assert.Equal(expected, FileTypeService.Instance.GetCategory(ext));
        }

        [Fact]
        public void SubtitleCompanion_PairsWithMovie()
        {
            var now = DateTime.Now;
            var movie = new FileItem { Name = "Film.mkv", FullPath = "/tmp/Film.mkv", ModifiedDate = now };
            movie.TargetFolder = TargetFolderResolver.Resolve(movie, OrganizationMode.Category, FolderFormat.YearMonth, "", "");
            var sub = new FileItem { Name = "Film.en.srt", FullPath = "/tmp/Film.en.srt", ModifiedDate = now };
            sub.TargetFolder = TargetFolderResolver.Resolve(sub, OrganizationMode.Category, FolderFormat.YearMonth, "", "");

            var items = new List<FileItem> { movie, sub };
            TargetFolderResolver.ApplySubtitleCompanionPairing(items);

            Assert.Equal(movie.TargetFolder, sub.TargetFolder);
        }

        [Fact]
        public void PrefixSuffix_WrapsCategoryName()
        {
            var item = new FileItem { Name = "app.json", FullPath = "/tmp/app.json", ModifiedDate = DateTime.Now };
            string result = TargetFolderResolver.Resolve(item, OrganizationMode.Category, FolderFormat.YearMonth, "", "", "Pre_", "_Post");

            Assert.Equal("Pre_JSON Files_Post", result);
        }

        [Fact]
        public void DateMode_UsesYearMonthFormat()
        {
            var item = new FileItem { Name = "a.txt", FullPath = "/tmp/a.txt", ModifiedDate = new DateTime(2026, 9, 22) };
            string folder = TargetFolderResolver.Resolve(item, OrganizationMode.Date, FolderFormat.YearMonth, "", "");

            Assert.Equal("2026 September", folder);
        }

        [Fact]
        public void ScanFiles_SkipsUnixDotfiles()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"tf-dotfile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "real.txt"), "x");
                File.WriteAllText(Path.Combine(dir, ".DS_Store"), "junk");
                File.WriteAllText(Path.Combine(dir, ".hidden"), "junk");

                var svc = new FileOrganizerService("TimeFold", dir, dir);
                var files = svc.ScanFiles(includeTopLevelFolders: false, ignoreSystemFiles: true);

                var names = files.Select(f => f.Name).ToList();
                Assert.Contains("real.txt", names);
                Assert.DoesNotContain(".DS_Store", names);
                Assert.DoesNotContain(".hidden", names);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Undo_RestoresFilesFromAuditLog()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"tf-undo-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                // Arrange: organizar una carpeta real con el core
                File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
                File.WriteAllText(Path.Combine(dir, "b.jpg"), "b");

                var svc = new FileOrganizerService("TimeFold", dir, dir);
                var scanned = svc.ScanFiles(false, true);
                var organizeTask = svc.OrganizeFilesAsync(scanned, null, CancellationToken.None);
                organizeTask.Wait();
                var organizeResult = organizeTask.Result;

                Assert.Equal(2, organizeResult.FilesMoved);
                Assert.True(File.Exists(organizeResult.CsvLogPath));
                Assert.Equal(0, Directory.EnumerateFileSystemEntries(dir).Count(e => !Path.GetFileName(e).StartsWith("Sorted_") && !Path.GetFileName(e).EndsWith(".csv")));

                // Act: undo desde el CSV
                var undo = UndoService.UndoFromLog(organizeResult.CsvLogPath);

                // Assert: archivos de vuelta, Sorted_ vacío eliminado
                Assert.Equal(2, undo.FilesRestored);
                Assert.Equal(0, undo.Errors);
                Assert.True(File.Exists(Path.Combine(dir, "a.txt")));
                Assert.True(File.Exists(Path.Combine(dir, "b.jpg")));
                Assert.Single(undo.RemovedFolders); // la carpeta Sorted_ quedó vacía y se eliminó
                Assert.Empty(Directory.GetDirectories(dir, "Sorted_*"));

                // Idempotente: un segundo undo no rompe nada
                var undo2 = UndoService.UndoFromLog(organizeResult.CsvLogPath);
                Assert.Equal(0, undo2.FilesRestored);
                Assert.Equal(2, undo2.FilesSkipped);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void ConfigDirectory_ResolvesToUserProfile()
        {
            string path = FileOrganizer.Config.AppConstants.GetConfigDirectoryPath();
            Assert.DoesNotContain("Windows", path);
            Assert.Contains("Appsphinx", path);
        }
    }
}
