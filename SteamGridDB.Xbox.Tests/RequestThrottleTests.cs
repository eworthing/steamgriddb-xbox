using System;

using SteamGridDB.Xbox.Services.SteamGridDB;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// What the client does when SteamGridDB says "not so fast".
    ///
    /// The behaviour being pinned here is a promise made to somebody else's server, which is exactly
    /// the kind that rots quietly: nothing in the app gets worse if the backoff stops working, so
    /// nothing would notice. Every decision is pure and takes its own <c>now</c>, so all of it is
    /// covered without a clock or a network - unlike the client that owns it.
    /// </summary>
    public class RequestThrottleTests
    {
        private static readonly DateTimeOffset now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

        [Theory]
        [InlineData(429)]
        [InlineData(503)]
        public void Recognises_the_codes_that_mean_slow_down(int statusCode)
        {
            Assert.True(RequestThrottle.IsThrottled(statusCode));
        }

        [Theory]
        [InlineData(200)]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(404)]
        [InlineData(500)]
        [InlineData(502)]
        public void Leaves_ordinary_failures_alone(int statusCode)
        {
            // A bad API key or a missing game is not made better by waiting, and treating either as
            // throttling would stop a whole run over one bad request.
            Assert.False(RequestThrottle.IsThrottled(statusCode));
        }

        [Fact]
        public void Reads_retry_after_as_a_number_of_seconds()
        {
            Assert.Equal(TimeSpan.FromSeconds(30), RequestThrottle.ParseRetryAfter("30", now));
        }

        [Fact]
        public void Reads_retry_after_as_an_http_date()
        {
            Assert.Equal(
                TimeSpan.FromSeconds(45),
                RequestThrottle.ParseRetryAfter("Fri, 07 Aug 2026 12:00:45 GMT", now));
        }

        [Fact]
        public void Treats_a_retry_after_date_already_past_as_now()
        {
            // The server named a moment and it has gone by. That is a real answer meaning "go ahead",
            // not a missing one - it must not fall through to the invented backoff.
            Assert.Equal(
                TimeSpan.Zero,
                RequestThrottle.ParseRetryAfter("Fri, 07 Aug 2026 11:59:00 GMT", now));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("soon")]
        [InlineData("-5")]
        public void Reports_nothing_usable_rather_than_a_bad_wait(string header)
        {
            // Null is the caller's cue to fall back on its own backoff. A negative delta in particular
            // must not become a negative wait, which would read as "no need to pause at all".
            Assert.Null(RequestThrottle.ParseRetryAfter(header, now));
        }

        [Fact]
        public void Waits_as_long_as_the_server_asked()
        {
            Assert.Equal(TimeSpan.FromSeconds(12), RequestThrottle.BackoffFor("12", now, 1));
        }

        [Fact]
        public void Backs_off_further_each_time_when_the_server_says_nothing()
        {
            Assert.Equal(RequestThrottle.BaseBackoff, RequestThrottle.BackoffFor(null, now, 1));
            Assert.Equal(TimeSpan.FromTicks(RequestThrottle.BaseBackoff.Ticks * 2), RequestThrottle.BackoffFor(null, now, 2));
            Assert.Equal(TimeSpan.FromTicks(RequestThrottle.BaseBackoff.Ticks * 4), RequestThrottle.BackoffFor(null, now, 3));
        }

        [Fact]
        public void Never_waits_longer_than_the_cap_even_when_asked_to()
        {
            // A Retry-After of an hour is a legitimate answer, but a widget that stops responding for
            // it reads as a hang. Past the cap, giving up is the honest outcome.
            Assert.Equal(RequestThrottle.MaxBackoff, RequestThrottle.BackoffFor("3600", now, 1));
            Assert.Equal(RequestThrottle.MaxBackoff, RequestThrottle.BackoffFor(null, now, 20));
        }

        [Fact]
        public void Holds_the_next_request_back_after_a_refusal()
        {
            var throttle = new RequestThrottle();

            Assert.Equal(TimeSpan.Zero, throttle.WaitBefore(now));

            throttle.ObserveThrottled("20", now);

            Assert.Equal(TimeSpan.FromSeconds(20), throttle.WaitBefore(now));
            Assert.Equal(TimeSpan.FromSeconds(5), throttle.WaitBefore(now + TimeSpan.FromSeconds(15)));
            Assert.Equal(TimeSpan.Zero, throttle.WaitBefore(now + TimeSpan.FromSeconds(20)));
        }

        [Fact]
        public void Gives_up_after_three_refusals_in_a_row()
        {
            var throttle = new RequestThrottle();

            throttle.ObserveThrottled(null, now);
            throttle.ObserveThrottled(null, now);

            Assert.False(throttle.HasGivenUp);

            throttle.ObserveThrottled(null, now);

            Assert.True(throttle.HasGivenUp);
        }

        [Fact]
        public void Starts_the_streak_over_when_the_server_answers_at_all()
        {
            // A 404 between two 429s is the service answering normally. It says nothing about how
            // often we are asking, so it must not count towards giving up.
            var throttle = new RequestThrottle();

            throttle.ObserveThrottled(null, now);
            throttle.ObserveThrottled(null, now);
            throttle.ObserveServed();
            throttle.ObserveThrottled(null, now);

            Assert.False(throttle.HasGivenUp);
            Assert.Equal(1, throttle.Consecutive);
        }

        [Fact]
        public void Clears_the_pending_wait_when_the_server_answers()
        {
            var throttle = new RequestThrottle();

            throttle.ObserveThrottled("60", now);
            throttle.ObserveServed();

            Assert.Equal(TimeSpan.Zero, throttle.WaitBefore(now));
        }

        [Fact]
        public void Keeps_refusing_once_it_has_given_up()
        {
            // Terminal for this client. A fix run holds one client and a library load holds another,
            // so the next run still starts fresh - but this one must not talk itself back into asking.
            var throttle = new RequestThrottle();

            throttle.ObserveThrottled(null, now);
            throttle.ObserveThrottled(null, now);
            throttle.ObserveThrottled(null, now);

            Assert.True(throttle.HasGivenUp);

            throttle.ObserveThrottled(null, now);

            Assert.True(throttle.HasGivenUp);
        }
    }
}
