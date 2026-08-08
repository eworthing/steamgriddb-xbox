using System;
using System.Threading;
using System.Threading.Tasks;

namespace SteamGridDB.Xbox.Services
{
    /// <summary>
    /// Loads a value once and remembers it, the way EpicLibrary and AppliedArtworkStore both needed
    /// to by hand: check for the value, take a lock only when it might be missing, check again in
    /// case another caller already loaded it while this one waited, then populate and release.
    ///
    /// Takes the lock rather than owning one, because a caller that also needs the same lock for its
    /// own later reads or writes - AppliedArtworkStore does, to keep GetAsync and UpdateAsync
    /// serialized against the same Dictionary instance - must keep using that one lock for its own
    /// lifetime, not a second lock this type would otherwise create for itself.
    /// </summary>
    /// <typeparam name="T">The loaded value's type. A reference type, so "not loaded yet" can be
    /// represented by null without a separate flag.</typeparam>
    internal sealed class AsyncLazyCache<T> where T : class
    {
        private readonly SemaphoreSlim gate;
        private readonly Func<Task<T>> loader;

        // Volatile because GetOrLoadAsync reads this outside the gate. Without it the unlocked read
        // is a plain load with no acquire semantics, so on a weakly ordered architecture a caller can
        // observe the non-null reference before the writes that populated the object it points at -
        // and every value loaded here is a Dictionary, whose buckets and entries arrays would then be
        // read half-initialised. Permitted on T because the type parameter is constrained to a
        // reference type.
        private volatile T value;

        /// <param name="gate">The lock to take while loading. Pass a lock the caller also uses for
        /// its own later access to the loaded value, or a dedicated one if nothing else needs it.</param>
        /// <param name="loader">Produces the value on first use. Runs at most once.</param>
        internal AsyncLazyCache(SemaphoreSlim gate, Func<Task<T>> loader)
        {
            this.gate = gate;
            this.loader = loader;
        }

        /// <summary>
        /// The loaded value, loading it first if no caller has yet.
        /// </summary>
        internal async Task<T> GetOrLoadAsync()
        {
            if (value != null)
            {
                return value;
            }

            await gate.WaitAsync();

            try
            {
                if (value != null)
                {
                    return value;
                }

                return value = await loader();
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
