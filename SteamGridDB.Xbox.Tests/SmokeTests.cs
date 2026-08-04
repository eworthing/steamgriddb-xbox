// `using System` is load-bearing, not tidiness: it brings in the extension methods that let an
// awaited WinRT IAsyncOperation compile. Without it every await below fails to build.
using System;
using System.IO;
using System.Threading.Tasks;

using Windows.Storage;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Proves the test host itself works before anything relies on it: that the linked app sources
    /// compile here, and that the WinRT storage APIs they use behave the same outside an app
    /// container as in. If these fail, no other failure in this project means anything.
    /// </summary>
    public class SmokeTests
    {
        [Fact]
        public async Task StorageFolder_works_outside_an_app_container()
        {
            using (var temp = new TempFolder())
            {
                StorageFile file = await temp.Folder.CreateFileAsync("tile.png", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, "bytes");

                Assert.Equal("bytes", await FileIO.ReadTextAsync(await temp.Folder.GetFileAsync("tile.png")));
            }
        }

        [Fact]
        public async Task Missing_file_throws_the_exception_the_app_catches()
        {
            using (var temp = new TempFolder())
            {
                // Every backup/restore path branches on this exact type. If the desktop projection
                // threw something else, those catch blocks would not fire and the tests would be
                // exercising a different program than the one that ships.
                await Assert.ThrowsAsync<FileNotFoundException>(
                    async () => await temp.Folder.GetFileAsync("absent.bak"));
            }
        }
    }
}
