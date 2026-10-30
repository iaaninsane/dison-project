using Godot;

// Câmara de partida no nó de entrada da fase (spec start-chamber.md). Reúne os terminais (o do modo da lanterna e o de
// liberar as saídas), montados no editor como filhos; o HexPhaseBuilder a põe no nó de entrada.
// Ao começar: deposita os créditos carregados e desliga a eclusa. Ao liberar: abre o nó, liga a eclusa, tira os
// terminais e avisa que a incursão começou.
public partial class StartChamber : Node2D
{
    [Signal] public delegate void IncursionStartedEventHandler(); // Para quem depende do início (ex.: spawners, 1.4)

    [Export] public HexPhaseBuilder Phase; // A fase (ligada no Inspector): abre as saídas do nó
    [Export] public Area2D Airlock;        // A eclusa (ligada no Inspector): só extrai depois de liberar

    public bool HasStarted { get; private set; } = false;

    public override void _Ready()
    {
        GameState.Instance?.DepositCarried(); // Chegou à câmara: guarda no banco os créditos carregados (como a SalaEntrada)
        SetAirlockActive(false);              // Com o nó travado, não há o que extrair
    }

    // Conectado pelo editor ao sinal Activated do terminal "Liberar saídas"
    public void OnExitsReleased()
    {
        if (HasStarted) return;

        HasStarted = true;
        Phase?.OpenEntryExits();
        SetAirlockActive(true);
        RemoveTerminals();
        EmitSignal(SignalName.IncursionStarted);
    }

    // A eclusa só percebe o jogador com o Monitoring ligado; adiado, para não mexer na física no meio de um passo
    private void SetAirlockActive(bool active) => Airlock?.SetDeferred(Area2D.PropertyName.Monitoring, active);

    // Os terminais saem da fase até o próximo reinício; a câmara fica (guarda o HasStarted)
    private void RemoveTerminals()
    {
        foreach (Node child in GetChildren()) child.QueueFree();
    }
}
