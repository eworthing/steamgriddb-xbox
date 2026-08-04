using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Load-once-and-remember, shared by the store name caches and the applied-artwork record.
    /// </summary>
    public class AsyncLazyCacheTests
    {
        [Fact]
        public async Task Loads_once_however_many_callers_arrive_together()
        {
            // The case the hand-written versions of this existed to handle: a library load fans out
            // across every game at once, and all of them reach an unloaded cache in the same instant.
            // Loading per caller would mean one read of the manifests per game.
            int loads = 0;

            var cache = new AsyncLazyCache<string>(new SemaphoreSlim(1, 1), async () =>
            {
                Interlocked.Increment(ref loads);

                await Task.Delay(20);

                return "loaded";
            });

            var callers = new List<Task<string>>();

            for (int i = 0; i < 32; i++)
            {
                callers.Add(Task.Run(() => cache.GetOrLoadAsync()));
            }

            string[] results = await Task.WhenAll(callers);

            Assert.Equal(1, loads);
            Assert.All(results, r => Assert.Equal("loaded", r));
        }

        [Fact]
        public async Task Hands_every_caller_the_same_instance()
        {
            // Callers mutate the loaded dictionary in place, so a second instance would silently
            // split the record in two.
            var cache = new AsyncLazyCache<List<string>>(new SemaphoreSlim(1, 1),
                () => Task.FromResult(new List<string>()));

            Assert.Same(await cache.GetOrLoadAsync(), await cache.GetOrLoadAsync());
        }

        [Fact]
        public async Task Does_not_reload_after_the_first_load()
        {
            int loads = 0;

            var cache = new AsyncLazyCache<string>(new SemaphoreSlim(1, 1),
                () => Task.FromResult((++loads).ToString()));

            await cache.GetOrLoadAsync();
            await cache.GetOrLoadAsync();
            await cache.GetOrLoadAsync();

            Assert.Equal(1, loads);
        }

        [Fact]
        public async Task Releases_the_lock_it_was_given_for_the_caller_to_use()
        {
            // It takes the caller's lock rather than owning one, because AppliedArtworkStore needs the
            // same lock afterwards to serialise its reads against its writes. Holding on to it would
            // deadlock the first read after a load.
            var gate = new SemaphoreSlim(1, 1);

            var cache = new AsyncLazyCache<string>(gate, () => Task.FromResult("loaded"));

            await cache.GetOrLoadAsync();

            Assert.Equal(1, gate.CurrentCount);

            await cache.GetOrLoadAsync();

            Assert.Equal(1, gate.CurrentCount);
        }
    }
}
