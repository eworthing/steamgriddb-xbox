using Xunit;

// AppliedArtworkStore and FixLog hold process-wide static state, and the tests point them at a
// different folder per test. Running test classes concurrently would let one test's folder assignment
// land in the middle of another's read. The whole suite finishes in well under a second, so serialising
// it costs nothing worth having.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
