using System;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace SteamGridDB.Xbox.Controls
{
    /// <summary>
    /// Turning a game's image file into the thumbnail the library list shows.
    ///
    /// This is the one step of a library load that cannot leave the app container: a
    /// <see cref="BitmapImage"/> is a Windows.UI.Xaml type owned by the UI thread, and the rest of
    /// the load - which entries exist, where their images are, what each game is called - is already
    /// under Services\ and tested. Keeping the decode here rather than in PrimaryWidget means the
    /// load can be extracted around it; the dispatcher it needs is passed in rather than reached
    /// through a page.
    /// </summary>
    internal static class ThumbnailDecoder
    {
        // Artwork is shown in an 80px list thumbnail; decoding the 512-1024px source at full size would
        // hold tens of megabytes of bitmaps for a large library. 160px covers 2x display scaling.
        private const int decodePixelWidth = 160;

        /// <summary>
        /// Decodes a game image at list-thumbnail size on the UI thread and releases the file handle
        /// as soon as decoding finishes.
        /// </summary>
        /// <param name="dispatcher">The UI thread's dispatcher.</param>
        /// <param name="file">Image file to decode.</param>
        /// <returns>The decoded image, or null when it could not be decoded.</returns>
        internal static async Task<BitmapImage> CreateAsync(CoreDispatcher dispatcher, StorageFile file)
        {
            IRandomAccessStream imageStream = await file.OpenReadAsync();

            try
            {
                // Every caller reaches this from a UI event handler, so decoding happens inline - no
                // dispatcher round trip that could leave the await hanging if the handler never runs
                if (dispatcher.HasThreadAccess)
                {
                    return await DecodeAsync(file, imageStream);
                }

                // BitmapImage must be created and sourced on the UI thread because it is owned by it
                TaskCompletionSource<BitmapImage> decoded = new TaskCompletionSource<BitmapImage>(TaskCreationOptions.RunContinuationsAsynchronously);

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    // The lambda is async void: an exception escaping it is lost to the dispatcher, so
                    // without this catch nothing would ever complete the source and the await below
                    // would hang forever - taking the library-operation guard with it, leaving every
                    // header button disabled and the widget stuck on its last status line with no
                    // error to explain it. Faulting the task instead surfaces it to the caller's own
                    // handler, the way the same failure on the inline path already does.
                    try
                    {
                        decoded.TrySetResult(await DecodeAsync(file, imageStream));
                    }
                    catch (Exception ex)
                    {
                        decoded.TrySetException(ex);
                    }
                });

                return await decoded.Task;
            }
            finally
            {
                imageStream.Dispose();
            }
        }

        /// <summary>
        /// Decodes an already-open image stream at thumbnail size. Must run on the UI thread.
        /// </summary>
        /// <returns>The decoded image, or null when it could not be decoded.</returns>
        private static async Task<BitmapImage> DecodeAsync(StorageFile file, IRandomAccessStream imageStream)
        {
            try
            {
                BitmapImage image = new BitmapImage { DecodePixelWidth = decodePixelWidth };

                await image.SetSourceAsync(imageStream);

                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not decode {file.Name}: {ex.Message}");

                return null;
            }
        }
    }
}
