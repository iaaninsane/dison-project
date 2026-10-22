using Godot;

// Depois da morte, cobra a taxa da biomassa carregada e, após um atraso, escurece a tela e troca de cena
// (a câmara inicial, ou recarrega a sala atual se nenhum destino for configurado).
public partial class RestartOnDeath : Node
{
    [Export] public float DeathRestartDelay = 2f;                            // Tempo entre a morte e a troca de cena (s)
    [Export(PropertyHint.File, "*.tscn")] public string ReturnScenePath = ""; // Para onde voltar; vazio = recarregar a sala
    [Export] public float DeathKeepFraction = 0.5f;                          // Fração da biomassa carregada guardada na morte

    // Conectado pelo editor ao sinal Died do SuitIntegrity
    public void OnPlayerDied()
    {
        GameState.Instance?.ApplyDeathPenalty(DeathKeepFraction);
        SceneTreeTimer timer = GetTree().CreateTimer(DeathRestartDelay);
        timer.Timeout += GoAfterDeath;
    }

    private void GoAfterDeath()
    {
        // Destino vazio = recarregar a sala atual (comportamento antigo)
        string scenePath = string.IsNullOrEmpty(ReturnScenePath) ? GetTree().CurrentScene.SceneFilePath : ReturnScenePath;
        ScreenFade.ChangeScene(this, scenePath);
    }
}
