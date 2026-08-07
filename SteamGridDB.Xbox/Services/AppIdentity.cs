using System;

using Windows.ApplicationModel;
using Windows.Web.Http.Headers;

namespace SteamGridDB.Xbox.Services
{
    /// <summary>
    /// How this app identifies itself to the services it calls.
    ///
    /// Every one of them - SteamGridDB, GOG, two community databases on GitHub, Microsoft's display
    /// catalogue - is someone else's server being used for free, and until now all four of the app's
    /// <c>HttpClient</c>s reached them under the WinRT default user agent, which says nothing about who
    /// is calling. That matters when something goes wrong at their end rather than ours: an operator
    /// looking at an unusual pattern of requests can throttle or contact a named client, but an
    /// anonymous one can only be dealt with by blocking whatever it has in common with everyone else.
    ///
    /// The form is the conventional one for a non-browser client - product, version, and a URL to
    /// follow - so it is recognisable in a log without being looked up.
    /// </summary>
    internal static class AppIdentity
    {
        private const string productName = "SteamGridDB.Xbox";
        private const string projectUrl = "https://github.com/eworthing/steamgriddb-xbox";

        /// <summary>
        /// Stands in when the package version cannot be read, which outside an app container it cannot -
        /// see <see cref="ResolveVersion"/>. Deliberately not a plausible version number: a request
        /// logged under this came from a test host or an unpackaged build, and saying so is more use
        /// than guessing.
        /// </summary>
        private const string unpackagedVersion = "unpackaged";

        private static string userAgent;

        /// <summary>
        /// The user agent every client sends, e.g.
        /// <c>SteamGridDB.Xbox/1.4.0 (+https://github.com/eworthing/steamgriddb-xbox)</c>.
        /// Built once; the package version cannot change while the process runs.
        /// </summary>
        internal static string UserAgent => userAgent ?? (userAgent = $"{productName}/{ResolveVersion()} (+{projectUrl})");

        /// <summary>
        /// Adds <see cref="UserAgent"/> to a client's default headers.
        ///
        /// Parsed rather than appended verbatim, and a parse failure is allowed to pass silently: going
        /// unidentified is exactly the behaviour this replaces, so it is not worth failing a client's
        /// construction - and therefore a library load - over a header.
        /// </summary>
        /// <param name="headers">The client's <c>DefaultRequestHeaders</c>.</param>
        internal static void Identify(HttpRequestHeaderCollection headers)
        {
            headers?.UserAgent.TryParseAdd(UserAgent);
        }

        /// <summary>
        /// The package's three-part version, or <see cref="unpackagedVersion"/> when there is no package
        /// to ask - <c>Package.Current</c> resolves only inside an app container, the same constraint
        /// that makes <c>ApplicationData.Current</c> a settable property on the stores.
        /// </summary>
        private static string ResolveVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;

                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read the package version: {ex.Message}");

                return unpackagedVersion;
            }
        }
    }
}
