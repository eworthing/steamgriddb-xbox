using System;
using System.IO;
using System.Threading.Tasks;

using Windows.Storage;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// A real directory on disk, exposed as the <see cref="StorageFolder"/> the app works in terms of,
    /// deleted when the test finishes.
    ///
    /// Nothing here is a fake. StorageFolder and StorageFile are sealed WinRT types with no interface
    /// to substitute, and the operations that matter - rename-over-existing, delete, collision
    /// options - are exactly where a mistake destroys artwork, so a stub of them would be testing the
    /// stub's opinion of Windows rather than Windows. The isolation comes from the directory being
    /// throwaway, not from the file system being faked.
    /// </summary>
    internal sealed class TempFolder : IDisposable
    {
        private readonly string path;

        internal TempFolder()
        {
            path = Path.Combine(Path.GetTempPath(), "sgdb-tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);

            Folder = StorageFolder.GetFolderFromPathAsync(path).AsTask().GetAwaiter().GetResult();
        }

        /// <summary>The directory, as the app sees it.</summary>
        internal StorageFolder Folder
        {
            get;
        }

        /// <summary>The directory as a plain path, for assertions that only need to know a file exists.</summary>
        internal string FullPath => path;

        /// <summary>Writes a file with the given text content, replacing any existing one.</summary>
        internal async Task<StorageFile> WriteAsync(string fileName, string content)
        {
            StorageFile file = await Folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, content);

            return file;
        }

        /// <summary>Writes a file with the given bytes, replacing any existing one.</summary>
        internal async Task<StorageFile> WriteBytesAsync(string fileName, byte[] content)
        {
            StorageFile file = await Folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteBytesAsync(file, content);

            return file;
        }

        /// <summary>The file's text content, or null when it does not exist.</summary>
        internal async Task<string> ReadAsync(string fileName)
        {
            try
            {
                return await FileIO.ReadTextAsync(await Folder.GetFileAsync(fileName));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        /// <summary>The file's bytes, or null when it does not exist.</summary>
        internal byte[] ReadBytes(string fileName)
        {
            string full = Path.Combine(path, fileName);

            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }

        /// <summary>Whether the file is present.</summary>
        internal bool Exists(string fileName)
        {
            return File.Exists(Path.Combine(path, fileName));
        }

        /// <summary>Every file name in the directory, for asserting nothing unexpected was left behind.</summary>
        internal string[] FileNames()
        {
            string[] names = Directory.GetFiles(path);

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = Path.GetFileName(names[i]);
            }

            Array.Sort(names, StringComparer.OrdinalIgnoreCase);

            return names;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                // A handle the test left open is not worth failing an otherwise passing test over;
                // the directory is under %TEMP% and named per-run, so a leftover collides with nothing.
            }
        }
    }
}
