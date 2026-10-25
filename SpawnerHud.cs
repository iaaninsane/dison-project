using Godot;

// HUD mínimo de teste: quantos spawners faltam e, no fim, "fase limpa" (spec mobile-spawners.md, S6 e S7).
// Só escuta os sinais do SpawnerDirector (Observer); não conhece o diretor.
public partial class SpawnerHud : Label
{
    public override void _Ready() => Text = ""; // Vazio até a incursão começar (na câmara não há spawners)

    // Conectado pelo editor ao sinal SpawnersChanged do SpawnerDirector
    public void OnSpawnersChanged(int remaining, int total) => Text = $"Spawners: {remaining}/{total}";

    // Conectado pelo editor ao sinal AllSpawnersDestroyed do SpawnerDirector
    public void OnAllSpawnersDestroyed() => Text = "Fase limpa: volte à eclusa";
}
