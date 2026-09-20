using Xunit;

// Every test class here opens the real sensor stack, and one machine's hardware cannot be opened
// by several Computer instances at once: run in parallel, the classes take each other's sensors
// away and a different test fails on every run, while each one passes on its own. Serialising the
// assembly is what makes the suite's verdict mean something.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
