using Godot;

// Depois da morte, espera um pouco e recarrega a sala atual (versão provisória do MORRER).
// Isolado para ser trocado quando a spec de evolução definir o fluxo final (ex.: voltar à câmara inicial).
public partial class RestartOnDeath : Node
{
    [Export] public float DeathRestartDelay = 2f; // Tempo entre a morte e o recarregamento (s)

    // Conectado pelo editor ao sinal Died do SuitIntegrity
    public void OnPlayerDied()
    {
        SceneTreeTimer timer = GetTree().CreateTimer(DeathRestartDelay);
        timer.Timeout += ReloadRoom;
    }

    private void ReloadRoom() => GetTree().ReloadCurrentScene();
}
