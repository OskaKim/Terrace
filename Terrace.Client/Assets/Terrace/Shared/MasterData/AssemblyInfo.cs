using MasterMemory;

// Unity では Source Generator が既定の名前空間を拾えないため、生成コードの名前空間を明示する。
// Builder 側(Terrace.MasterData.Builder)と同じ Terrace.MasterData に揃える。
[assembly: MasterMemoryGeneratorOptions(Namespace = "Terrace.MasterData")]
