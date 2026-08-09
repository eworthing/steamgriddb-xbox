// System is not decorative here: it carries the extension method that makes `await` work on the
// WinRT IAsyncOperation the StorageFolder calls below return. See TESTING.md.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Xbox;

using Windows.Storage;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Indexing the cache must not leave the files it read open.
    ///
    /// The index is built immediately before artwork is written over the very files it just walked, so
    /// a handle left behind by the walk is a handle the write then collides with. Nothing about that
    /// failure points back here: it surfaces as a write denied on one rendition, in a different module,
    /// on the largest file - which is the one the tile is actually made of.
    /// </summary>
    public class ImageCacheIndexHandleTests
    {
        [Fact]
        public async Task Indexing_leaves_no_handle_on_the_files_it_read()
        {
            using (var cache = new TempFolder())
            {
                StorageFile file = await cache.Folder.CreateFileAsync("tile");

                await FileIO.WriteBufferAsync(file, await TestImages.PngAsync(329, 329));

                List<ImageCacheIndex.CachedImage> index = await ImageCacheIndex.BuildAsync(cache.Folder);

                Assert.Single(index);

                // Exactly what ArtworkFiles.ApplyEncodedAsync does to a rendition after the index that
                // found it has been built
                StorageFile written = await cache.Folder.CreateFileAsync("tile", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteBufferAsync(written, await TestImages.PngAsync(329, 329));
            }
        }
    }
}
