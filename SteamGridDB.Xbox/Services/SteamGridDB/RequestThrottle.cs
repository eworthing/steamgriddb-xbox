using System;
using System.Globalization;

namespace SteamGridDB.Xbox.Services.SteamGridDB
{
    /// <summary>
    /// What to do when SteamGridDB says "not so fast".
    ///
    /// Before this, it could not say so at all: <see cref="SteamGridDbClient"/> collapsed every
    /// non-success response into null, so a 429 was indistinguishable from a 404, and the fix loop
    /// moved straight on to the next game at full speed. A library of two hundred games would keep
    /// firing for the entire throttle window, which is the one situation where the app's request
    /// pattern stops being merely heavy and starts being rude - the server has explicitly asked for a
    /// pause and is being ignored.
    ///
    /// Two responses, both deliberately conservative:
    ///
    /// <list type="bullet">
    /// <item>pace the <em>next</em> request, honouring <c>Retry-After</c> when one is offered and
    /// backing off exponentially when it is not. The throttled request is never retried - retrying is
    /// how a backoff turns into an amplifier - so the game it belonged to is simply counted as an
    /// error, which is what the null contract on <see cref="SteamGridDbClient"/> already meant.</item>
    /// <item>after <see cref="GiveUpAfterConsecutive"/> throttled responses in a row, stop entirely.
    /// A run that is being refused this persistently will not produce a good library, and continuing
    /// to ask only makes the refusal worse.</item>
    /// </list>
    ///
    /// All of the decision-making is pure and takes <c>now</c> as an argument, so it is covered by
    /// RequestThrottleTests without a clock or a network - unlike the client that owns it, which is
    /// the network I/O TESTING.md carves out.
    /// </summary>
    internal sealed class RequestThrottle
    {
        /// <summary>
        /// How many throttled responses in a row before the client stops asking. Three rather than one
        /// because a single 429 is routine on a shared API and the backoff alone usually clears it;
        /// three in a row, each after its own wait, is the server meaning it.
        /// </summary>
        internal const int GiveUpAfterConsecutive = 3;

        /// <summary>Wait after the first throttled response that carried no <c>Retry-After</c>.</summary>
        internal static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The longest this will ever hold a request back, whatever <c>Retry-After</c> asks for. A
        /// widget that stops responding for the ten minutes a server is entitled to name reads as a
        /// hang; past this point giving up (below) is the honest outcome.
        /// </summary>
        internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

        private DateTimeOffset resumeAt;
        private int consecutive;

        /// <summary>
        /// Throttled responses seen in a row. Reset by any response the server actually served,
        /// including a 404 - that is the service answering normally, not refusing.
        /// </summary>
        internal int Consecutive => consecutive;

        /// <summary>
        /// Whether the client has stopped asking. Terminal for this client's lifetime: a fix run holds
        /// one client and a library load holds another, so the next run starts fresh.
        /// </summary>
        internal bool HasGivenUp => consecutive >= GiveUpAfterConsecutive;

        /// <summary>
        /// How long to hold the next request back, or zero when it can go out now.
        /// </summary>
        /// <param name="now">Current time.</param>
        internal TimeSpan WaitBefore(DateTimeOffset now)
        {
            return resumeAt > now ? resumeAt - now : TimeSpan.Zero;
        }

        /// <summary>
        /// Records a response the server refused to serve.
        /// </summary>
        /// <param name="retryAfter">The response's <c>Retry-After</c> header, or null when it had none.</param>
        /// <param name="now">Current time.</param>
        internal void ObserveThrottled(string retryAfter, DateTimeOffset now)
        {
            consecutive++;
            resumeAt = now + BackoffFor(retryAfter, now, consecutive);
        }

        /// <summary>
        /// Records a response the server served, whatever its status. Clears the backoff: the point of
        /// the streak is to notice sustained refusal, and one served response says there is none.
        /// </summary>
        internal void ObserveServed()
        {
            consecutive = 0;
            resumeAt = default(DateTimeOffset);
        }

        /// <summary>
        /// Whether a status code means "you are asking too often" rather than "that request was wrong".
        /// 429 is the explicit one; 503 is the other response that carries <c>Retry-After</c> and means
        /// come back later. Ordinary 4xx and the remaining 5xx stay plain failures - they are not made
        /// better by waiting, and treating them as throttling would stop a run over a bad request.
        /// </summary>
        /// <param name="statusCode">HTTP status code of the response.</param>
        internal static bool IsThrottled(int statusCode)
        {
            return statusCode == 429 || statusCode == 503;
        }

        /// <summary>
        /// How long to wait after a throttled response: what the server asked for if it asked for
        /// anything usable, otherwise <see cref="BaseBackoff"/> doubled once per consecutive refusal.
        /// Always clamped to <see cref="MaxBackoff"/>, including the server's own figure - a
        /// <c>Retry-After</c> of an hour is a real answer, but not one a widget can sit through.
        /// </summary>
        /// <param name="retryAfter">The response's <c>Retry-After</c> header, or null.</param>
        /// <param name="now">Current time, for the HTTP-date form of the header.</param>
        /// <param name="consecutiveThrottles">How many refusals in a row this one makes, counting from 1.</param>
        internal static TimeSpan BackoffFor(string retryAfter, DateTimeOffset now, int consecutiveThrottles)
        {
            TimeSpan? asked = ParseRetryAfter(retryAfter, now);

            // Shift capped well below the point of overflow; MaxBackoff bites long before it anyway
            int doublings = Math.Max(0, Math.Min(consecutiveThrottles - 1, 8));
            TimeSpan backoff = asked ?? TimeSpan.FromTicks(BaseBackoff.Ticks << doublings);

            if (backoff > MaxBackoff)
            {
                return MaxBackoff;
            }

            return backoff < TimeSpan.Zero ? TimeSpan.Zero : backoff;
        }

        /// <summary>
        /// Reads a <c>Retry-After</c> header, which RFC 7231 allows in two forms: a whole number of
        /// seconds, or an HTTP date to wait until. Returns null when there is nothing usable there,
        /// which is the caller's cue to fall back on its own backoff rather than to skip waiting.
        ///
        /// A date already in the past yields zero rather than null: the server named a moment and it
        /// has passed, which is a real answer meaning "now", not a missing one.
        /// </summary>
        /// <param name="value">Header value, or null.</param>
        /// <param name="now">Current time, against which the date form is measured.</param>
        internal static TimeSpan? ParseRetryAfter(string value, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();

            // NumberStyles.None rather than Integer: it rejects a leading sign, so a negative delta
            // falls through to the date form and then to null instead of becoming a negative wait
            if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            if (DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset until))
            {
                return until > now ? until - now : TimeSpan.Zero;
            }

            return null;
        }
    }
}
