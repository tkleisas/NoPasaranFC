using Xunit;

// The engine shares static state (GameSettings.Instance, AIController.DeterministicSeedBase,
// tuning overrides) across tests; parallel execution makes seeds and settings race.
// Serialize the whole suite for determinism.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
