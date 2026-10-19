using Xunit;

// Disable test parallelization across test classes to avoid WPF PackagePart / BAML loading stream race conditions
[assembly: CollectionBehavior(DisableTestParallelization = true)]
