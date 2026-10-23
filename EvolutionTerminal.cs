using Godot;

// Terminal de evolução da câmara inicial: com o jogador dentro, a tecla de interação abre o painel de compra.
public partial class EvolutionTerminal : Area2D
{
    [Export] public UpgradePanel Panel; // Painel de compra (ligado no Inspector)

    private bool _playerInside = false;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    // Reage ao EVENTO de interação, não à leitura da tecla. O "A" que aperta "Fechar" não reabre o painel porque,
    // com o painel aberto, o jogo fica pausado (este terminal não recebe eventos) e o Panel.Visible ainda é verdadeiro.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("interact")) return;
        if (!_playerInside) return;
        if (Panel == null || Panel.Visible) return;

        Panel.Open();
        GetViewport().SetInputAsHandled(); // Este aperto já foi usado: ninguém mais reage a ele
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player) _playerInside = true;
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player) _playerInside = false;
    }
}
