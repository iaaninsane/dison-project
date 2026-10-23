using Godot;

// Terminal "Liberar saídas" da câmara de partida (spec start-chamber.md): com o jogador dentro, a interação avisa
// uma vez (sinal Activated). Ele não sabe o que acontece depois: quem escuta é a câmara de partida (Observer).
public partial class ExitReleaseTerminal : Area2D
{
    [Signal] public delegate void ActivatedEventHandler();

    private bool _playerInside = false;
    private bool _used = false; // Libera uma vez só

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    // Reage ao EVENTO de interação, não à leitura da tecla: com o jogo pausado (painel de evolução aberto), o evento
    // não chega aqui, e cada aperto vale uma vez, mesmo que aperto e soltura caiam no mesmo quadro
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("interact")) return;
        if (!_playerInside || _used) return;

        _used = true;
        GetViewport().SetInputAsHandled(); // Este aperto já foi usado: ninguém mais reage a ele
        EmitSignal(SignalName.Activated);
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
