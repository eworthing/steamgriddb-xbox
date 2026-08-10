using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Streams;

using SteamGridDB.Xbox.Services.Artwork;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// Applies, restores and re-applies artwork across every cached rendition of one first-party game.
    ///
    /// A third-party game is one file, so the widget's whole pipeline is built around one. A
    /// first-party game is several - the Xbox app fetched its artwork once per surface size - and the
    /// tile only changes everywhere if all of them do. This is where that fan-out lives, so nothing
    /// above it has to know a game can be more than one file.
    ///
    /// Each rendition is re-encoded to its own size, read from the file that is already there. The size
    /// is not recorded anywhere and not guessed: a rendition the Xbox app has since deleted is skipped
    /// rather than recreated, because the app tracks its cache in a database of its own and a file it
    /// has no row for is one it will remove again.
    /// </summary>
    internal static class XboxTiles
    {
        /// <summary>
        /// Stands in for a game with no recorded renditions, so every loop below can take a null list
        /// without allocating a throwaway one apiece to walk zero times.
        /// </summary>
        private static readonly string[] EmptyNames = Array.Empty<string>();

        /// <summary>
        /// Writes artwork over every rendition of a game's tile, preserving the Xbox app's originals the
        /// first time.
        ///
        /// A rendition that cannot be written costs that rendition and no more. These files live in the
        /// Xbox app's own live cache, so a write can be refused for reasons that have nothing to do with
        /// this game and everything to do with what the app happens to be doing at that moment - the
        /// same reason the library load catches per game around
        /// <see cref="ReapplyOverwrittenAsync"/>. Letting the first refusal out of here abandons the
        /// renditions after it, and because the largest is written first, the one most likely to be busy
        /// is the one whose failure would take all the others with it.
        ///
        /// The refusals are returned rather than swallowed. A partial write is a real outcome and the
        /// caller has to be able to say so: some of the surfaces showing this game changed and some did
        /// not, which to anyone looking at their library is indistinguishable from nothing happening.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="renditionFileNames">The game's cached images, largest first.</param>
        /// <param name="artworkBytes">The artwork to apply, in any format a decoder can read.</param>
        /// <returns>
        /// How many renditions were written, why any of the rest were refused, and whether backups of
        /// the originals now exist.
        /// </returns>
        internal static async Task<(int Written, IReadOnlyList<string> Failures, bool HasBackup)> ApplyAsync(
            StorageFolder cacheFolder,
            IReadOnlyList<string> renditionFileNames,
            IBuffer artworkBytes)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();
            List<string> failures = new List<string>();
            int written = 0;
            bool anyBackup = false;

            // Every rendition's size and the room its own file has, read from the files that are
            // already there - nothing about this pass touches the artwork.
            var renditions = new List<(string FileName, int Size, uint Room)>();

            foreach (string fileName in renditionFileNames ?? EmptyNames)
            {
                (int size, uint room) = await RenditionAsync(cacheFolder, fileName);

                if (size > 0)
                {
                    renditions.Add((fileName, size, room));
                }
            }

            // The artwork is decoded and its largest frame read exactly once here, no matter how many
            // renditions there are - see the class doc - and every rendition's square is cut from that
            // one frame and encoded to its own size and byte budget. This has to stay separate from the
            // write loop below: WithDecoderAsync's blanket catch must not be the thing standing between
            // one rendition's write failure and the rest of them, which is the whole reason ApplyAsync
            // contains its writes in the first place.
            var noArtwork = new List<(string FileName, int Size, uint Room, IBuffer TileBytes)>();

            foreach ((string fileName, int size, uint room) in renditions)
            {
                noArtwork.Add((fileName, size, room, null));
            }

            List<(string FileName, int Size, uint Room, IBuffer TileBytes)> encoded = await TileImage.WithDecoderAsync(
                artworkBytes,
                async decoder =>
                {
                    IBitmapFrame frame = await TileImage.LargestFrameAsync(decoder);
                    var results = new List<(string FileName, int Size, uint Room, IBuffer TileBytes)>();

                    foreach ((string fileName, int size, uint room) in renditions)
                    {
                        results.Add((fileName, size, room, await TileImage.EncodeSquareJpegFromFrameAsync(frame, size, room)));
                    }

                    return results;
                },
                // If the artwork itself will not decode, every rendition still needs its own failure
                // line below - a decode failure that produced one entry total left the caller with
                // written == 0 and no failures either, which reports "no tile to write to" instead of
                // why nothing was written.
                noArtwork,
                "Could not encode a tile from this artwork");

            foreach ((string fileName, int size, uint room, IBuffer tileBytes) in encoded)
            {
                if (tileBytes == null)
                {
                    // The tile is written without resizing the file, so the Store's own download sets how
                    // many bytes there are to work with - see ArtworkFiles.WriteMode
                    failures.Add($"{size}px (this artwork will not compress into the {room} bytes the cached tile has room for)");

                    continue;
                }

                try
                {
                    anyBackup |= await ArtworkFiles.ApplyEncodedAsync(
                        cacheFolder, fileName, vault, tileBytes, ArtworkFiles.WriteMode.InPlace);

                    written++;
                }
                catch (Exception ex)
                {
                    // Named by size rather than by file name, because the name is a hash of the request
                    // that fetched it and says nothing to anyone reading it
                    failures.Add($"{size}px ({ex.GetType().Name}: {ex.Message})");
                }
            }

            return (written, failures, anyBackup);
        }

        /// <summary>
        /// Puts the Xbox app's own artwork back across every rendition.
        ///
        /// A rendition that cannot be written costs that rendition and no more, for the same reason
        /// <see cref="ApplyAsync"/> contains its writes: these files live in the app's own live cache,
        /// the largest - the one most likely to be busy - comes first, and letting its refusal out of
        /// here would abandon the renditions behind it. A refused rendition keeps both its sidecars,
        /// so the retry has everything this attempt had.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="renditionFileNames">The game's cached images.</param>
        /// <returns>
        /// Restored when at least one original was put back, BackupMissing when none was, and why any
        /// rendition was refused. BackupMissing alongside failures means the backups exist and every
        /// write was refused - which a caller must not report as there being nothing to restore.
        /// </returns>
        internal static async Task<(ArtworkFiles.RestoreOutcome Outcome, IReadOnlyList<string> Failures)> RestoreAsync(
            StorageFolder cacheFolder,
            IReadOnlyList<string> renditionFileNames)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();
            List<string> failures = new List<string>();
            bool restoredAny = false;

            foreach (string fileName in renditionFileNames ?? EmptyNames)
            {
                try
                {
                    ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(
                        cacheFolder, fileName, vault, ArtworkFiles.WriteMode.InPlace);

                    restoredAny |= outcome == ArtworkFiles.RestoreOutcome.Restored;
                }
                catch (Exception ex)
                {
                    failures.Add($"{ex.GetType().Name}: {ex.Message}");
                }
            }

            return (
                restoredAny ? ArtworkFiles.RestoreOutcome.Restored : ArtworkFiles.RestoreOutcome.BackupMissing,
                failures);
        }

        /// <summary>
        /// Puts saved customisations back over any rendition the Xbox app has overwritten.
        ///
        /// Run on every library load, because for first-party tiles being overwritten is the expected
        /// course of events rather than an accident: the app re-downloads a cached image whenever the
        /// file goes missing or its ninety-day lifetime runs out, and it keeps no checksum, size or
        /// validator that would let it notice the file had been replaced in the meantime.
        ///
        /// Renditions that already hold the customisation are left alone, so a load that has nothing to
        /// do writes nothing.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="renditionFileNames">The game's cached images.</param>
        /// <param name="vaultFileNames">A listing of the vault, from <see cref="XboxTileStore.VaultFileNamesAsync"/>.</param>
        /// <returns>
        /// Reapplied when the game's saved customisation is on its tile, whether this call had to put
        /// it there or found it already in place; NothingSaved when no rendition has one saved. The
        /// distinction that matters to a caller is "is there a customisation" rather than "did this
        /// call write" - a load runs this on every game, so by the time a bulk restore reaches one, the
        /// answer to the second is always no.
        /// </returns>
        internal static async Task<ArtworkFiles.ReapplyOutcome> ReapplyOverwrittenAsync(
            StorageFolder cacheFolder,
            IReadOnlyList<string> renditionFileNames,
            ISet<string> vaultFileNames)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();
            bool anySaved = false;

            foreach (string fileName in renditionFileNames ?? EmptyNames)
            {
                // The listing answers "is anything saved for this rendition" without touching the
                // disk, and for a game nobody has customised - most of them, on most machines - that
                // is the entire answer. Both size reads below and the reapply attempt after them are
                // skipped, which is where nearly all of this pass's file I/O used to go.
                if (!vaultFileNames.Contains(ArtworkFiles.CustomisedNameFor(fileName)))
                {
                    continue;
                }

                try
                {
                    if (await MatchesSavedCustomisationAsync(cacheFolder, vault, fileName))
                    {
                        anySaved = true;

                        continue;
                    }

                    if (await ArtworkFiles.ReapplyCustomisationAsync(cacheFolder, fileName, vault, ArtworkFiles.WriteMode.InPlace)
                        == ArtworkFiles.ReapplyOutcome.Reapplied)
                    {
                        anySaved = true;
                    }
                }
                catch (Exception ex)
                {
                    // A rendition the cache refuses this load is a stale tile until the next one, not
                    // a lost game - the same containment ApplyAsync and RestoreAsync give their writes.
                    // The .new is untouched by a failed write, so the next load simply tries again.
                    System.Diagnostics.Debug.WriteLine(
                        $"Could not reapply the customisation of {fileName}: {ex.Message}");
                }
            }

            return anySaved ? ArtworkFiles.ReapplyOutcome.Reapplied : ArtworkFiles.ReapplyOutcome.NothingSaved;
        }

        /// <summary>
        /// Drops the saved customisations of renditions that have left the cache.
        ///
        /// The Xbox app re-fetches an evicted rendition under the same name - the name is a hash of the
        /// request, and the request has not changed - so a .new left behind for one is written straight
        /// back over it the next time the game is loaded, putting back artwork that may since have been
        /// reverted or replaced. The .bak is deliberately kept: it holds the Xbox app's own artwork,
        /// which is exactly what the re-fetch brings back, so it costs nothing and is the one file here
        /// that cannot be recreated.
        /// </summary>
        /// <param name="renditionFileNames">Cache file names that are no longer there.</param>
        internal static async Task DiscardSavedCustomisationsAsync(IEnumerable<string> renditionFileNames)
        {
            StorageFolder vault = null;

            foreach (string fileName in renditionFileNames ?? EmptyNames)
            {
                vault = vault ?? await XboxTileStore.VaultFolderAsync();

                try
                {
                    StorageFile saved = await vault.GetFileAsync(ArtworkFiles.CustomisedNameFor(fileName));

                    await saved.DeleteAsync();
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException)
                {
                    // Nothing saved for this rendition, which is the common case
                }
            }
        }

        /// <summary>
        /// Whether this game has backups, and so can be reverted to the Xbox app's own artwork.
        ///
        /// Answered from a listing rather than from the disk, which is what makes it synchronous - and
        /// therefore a plain function a test can call, rather than another awaited walk that could only
        /// be checked by running the app. A backup is a file existing under a known name, and a
        /// listing already says which names exist.
        /// </summary>
        /// <param name="renditionFileNames">The game's cached images.</param>
        /// <param name="vaultFileNames">A listing of the vault, from <see cref="XboxTileStore.VaultFileNamesAsync"/>.</param>
        internal static bool HasBackup(IReadOnlyList<string> renditionFileNames, ISet<string> vaultFileNames)
        {
            if (vaultFileNames == null)
            {
                return false;
            }

            foreach (string fileName in renditionFileNames ?? EmptyNames)
            {
                if (vaultFileNames.Contains(ArtworkFiles.BackupNameFor(fileName)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Drops the applied-artwork records of renditions that cannot be carrying a customisation.
        ///
        /// That record is what marks an artwork <em>In use</em> in the picker, and nothing else knows
        /// it - a tile on disk is just an image. It is keyed by full path, so a first-party game's
        /// record belongs to whichever rendition was its largest at the time, and two things leave one
        /// behind that no longer describes anything:
        ///
        /// <list type="bullet">
        /// <item>a rendition stops being part of the game's set, so nothing ever looks its path up
        /// again - not a revert, which only visits games it can restore, and not a later customisation,
        /// which writes under whatever the largest rendition is by then</item>
        /// <item>the game has no backup at all. <see cref="ApplyAsync"/> takes one before it writes, so
        /// a customisation always leaves a backup behind it; no backup therefore means no
        /// customisation, whatever the record says</item>
        /// </list>
        ///
        /// Only ever removes the record. The tile, the backup and the saved customisation are all left
        /// exactly as they are - this corrects a claim about them, and a wrong claim is worth less than
        /// the artwork it describes.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder, which the record is keyed under.</param>
        /// <param name="renditionFileNames">Cache file names whose records should go.</param>
        internal static async Task ForgetArtworkRecordsAsync(StorageFolder cacheFolder, IEnumerable<string> renditionFileNames)
        {
            if (cacheFolder == null)
            {
                return;
            }

            foreach (string fileName in renditionFileNames ?? EmptyNames)
            {
                await AppliedArtworkStore.ClearAsync(Path.Combine(cacheFolder.Path, fileName));
            }
        }

        /// <summary>
        /// Whether an applied-artwork record names a cached image no game claims, and so describes
        /// nothing at all.
        ///
        /// The two rules in <see cref="ForgetArtworkRecordsAsync"/> both work forwards from a game to
        /// its renditions, which is why neither reaches a record whose file has stopped being any
        /// game's rendition: nothing enumerates it any more, so nothing thinks to clear it. This is
        /// the reverse direction - start from the record and ask whether anything still claims it.
        ///
        /// Two guards keep it from over-reaching, and both matter more than the rule itself:
        ///
        /// <list type="bullet">
        /// <item>only records inside the Xbox app's image cache are judged. Third-party records live
        /// under ThirdPartyLibraries and are none of this method's business - it has no idea what
        /// accounts for those, and a rule that guesses would delete them all.</item>
        /// <item>an empty set of tracked renditions means "nothing known", never "nothing claimed".
        /// A damaged or missing tile record would otherwise make every first-party record look
        /// orphaned at once, which is the one way this could do real harm.</item>
        /// </list>
        /// </summary>
        /// <param name="recordKey">An applied-artwork key - the full path of the image it describes.</param>
        /// <param name="cacheFolderPath">The Xbox app's image cache folder.</param>
        /// <param name="trackedRenditions">Every cached image some game claims; empty means unknown.</param>
        internal static bool IsOrphanedRecord(string recordKey, string cacheFolderPath, ISet<string> trackedRenditions)
        {
            if (string.IsNullOrEmpty(recordKey)
                || string.IsNullOrEmpty(cacheFolderPath)
                || trackedRenditions == null
                || trackedRenditions.Count == 0)
            {
                return false;
            }

            // The record's key is lowercased on the way in and the folder's path is not, so neither
            // side of this can be compared as written
            if (!string.Equals(
                Path.GetDirectoryName(recordKey),
                cacheFolderPath.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !trackedRenditions.Contains(Path.GetFileName(recordKey));
        }

        /// <summary>
        /// A cached rendition's pixel size and its length on disk, or zeroes when it is gone or
        /// unreadable.
        ///
        /// The pixel size says what to encode the tile as; the length says how many bytes there are to
        /// put it in, which matters because the file is overwritten without being resized. Read from the
        /// file's header and its directory entry rather than its full bytes - the same header-only
        /// pattern <see cref="ImageCacheIndex.BuildAsync"/> uses, for the same reason: this runs once per
        /// rendition and only two numbers come out of it.
        /// </summary>
        private static async Task<(int Size, uint Room)> RenditionAsync(StorageFolder cacheFolder, string fileName)
        {
            StorageFile file;
            uint room;
            IRandomAccessStream stream;

            try
            {
                file = await cacheFolder.GetFileAsync(fileName);
                room = (uint)(await file.GetBasicPropertiesAsync()).Size;
                stream = await file.OpenReadAsync();
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException)
            {
                return (0, 0);
            }

            // Disposed before this returns, and well before ApplyAsync's write loop reaches the same
            // file - a handle left open here is exactly the collision ImageCacheIndexHandleTests exists
            // to catch, just one module over.
            using (stream)
            {
                try
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    return ((int)decoder.PixelWidth, room);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not measure cached rendition {fileName}: {ex.Message}");

                    return (0, room);
                }
            }
        }

        /// <summary>
        /// Whether a rendition already holds the bytes that were last applied to it.
        ///
        /// Compared by content, which it did not always have to be. A tile used to be written as a file
        /// of its own length, so a length that still matched the saved customisation's was proof enough
        /// and the directory entry answered it without reading anything. Tiles are now written without
        /// resizing the file - see <see cref="ArtworkFiles.WriteMode"/> - which means a customised tile
        /// and the Xbox app's own download are the same length as each other and always will be. Length
        /// stopped being evidence, and kept only the appearance of it: the check would have reported
        /// every overwritten tile as intact, and put nothing back.
        ///
        /// Reached only for a rendition the caller's vault listing says has a customisation saved at
        /// all, so a library nobody has customised reads nothing.
        /// </summary>
        private static async Task<bool> MatchesSavedCustomisationAsync(StorageFolder cacheFolder, StorageFolder vault, string fileName)
        {
            IBuffer current = await ReadIfPresentAsync(cacheFolder, fileName);
            IBuffer saved = await ReadIfPresentAsync(vault, ArtworkFiles.CustomisedNameFor(fileName));

            return current != null
                && saved != null
                && current.Length == saved.Length
                && CryptographicBuffer.Compare(current, saved);
        }

        /// <summary>
        /// A file's bytes, or null when it is not there.
        /// </summary>
        private static async Task<IBuffer> ReadIfPresentAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await FileIO.ReadBufferAsync(await folder.GetFileAsync(fileName));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
