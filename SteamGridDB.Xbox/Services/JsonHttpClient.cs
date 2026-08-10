using System;
using System.Threading;
using System.Threading.Tasks;

using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace SteamGridDB.Xbox.Services
{
    /// <summary>
    /// Builds one of this app's four <c>HttpClient</c>s the way all of them need to be built - stamped
    /// with <see cref="AppIdentity.UserAgent"/>, and optionally declaring it wants JSON back - and
    /// releases it the same way every owner already did.
    ///
    /// This is construction and disposal only. What happens in between stays with the caller, because
    /// three of the four need something this type does not offer: <c>SteamGridDbClient</c> adds an
    /// Authorization header and layers <c>RequestThrottle</c> over its own request method rather than
    /// this type's <see cref="GetStringAsync"/> - its manners are a promise to SteamGridDB specifically,
    /// not something a shared type should own. <c>StoreCatalog</c> adds a correlation-vector header and
    /// is the one caller whose request shape matches <see cref="GetStringAsync"/> exactly.
    /// <c>ArtworkDownloader</c> and <c>StoreNameLookup</c> reach past this type for <see cref="Client"/>
    /// because what they fetch - image bytes, or a handful of unrelated store and database endpoints -
    /// does not.
    /// </summary>
    internal class JsonHttpClient : IDisposable
    {
        private bool disposed;

        /// <summary>
        /// The underlying client. Exposed for callers whose request shape does not match
        /// <see cref="GetStringAsync"/>, and for adding a caller-specific default header before the
        /// first request - the way <c>SteamGridDbClient</c> adds Authorization and
        /// <c>StoreCatalog</c> adds MS-CV.
        /// </summary>
        internal HttpClient Client { get; }

        /// <param name="acceptJson">
        /// Whether to declare the client wants JSON back. False for ArtworkDownloader, which only ever
        /// downloads image bytes, and for StoreNameLookup, whose endpoints never relied on this header
        /// being set; true for SteamGridDbClient and StoreCatalog, which parse a JSON body on every
        /// request.
        /// </param>
        internal JsonHttpClient(bool acceptJson)
        {
            Client = new HttpClient();

            if (acceptJson)
            {
                Client.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
            }

            AppIdentity.Identify(Client.DefaultRequestHeaders);
        }

        /// <summary>
        /// GET returning the raw response body, or null on any failure - a timeout and a status check,
        /// nothing more. This is <c>StoreCatalog</c>'s own request method from before this type existed,
        /// moved rather than copied, because it is the one caller whose needs stop exactly here.
        /// <c>SteamGridDbClient</c>'s otherwise near-identical method also paces requests and counts
        /// unanswered ones - see this type's own remarks for why that stays where it is.
        /// </summary>
        /// <param name="url">URL to GET.</param>
        /// <param name="timeout">Per-request timeout.</param>
        /// <param name="cancellationToken">Caller's cancellation token, linked to the timeout.</param>
        internal async Task<string> GetStringAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                using (CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout))
                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    HttpResponseMessage response = await Client.GetAsync(new Uri(url)).AsTask(linkedCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync().AsTask(linkedCts.Token);
                    }

                    System.Diagnostics.Debug.WriteLine($"Store catalogue error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Store catalogue exception: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Releases the underlying client.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Client?.Dispose();
                disposed = true;
            }
        }
    }
}
