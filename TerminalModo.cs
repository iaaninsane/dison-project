using Godot;
using System;

public partial class TerminalModo : Area2D
{
    private ColorRect _visualRect;
    private bool _playerInside = false;

    public override void _Ready()
    {
        _visualRect = GetNodeOrNull<ColorRect>("ColorRect");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // Ao carregar a câmara, o terminal já mostra o modo guardado (antes ficava branco até o primeiro aperto)
        if (GameState.Instance == null) return;
        UpdateModeColor(GameState.Instance.Data.IsManualFire);
    }

    // Reage ao EVENTO de interação, não à leitura da tecla (polling): com o jogo pausado (painel de evolução aberto),
    // o evento não chega aqui; lendo a tecla, um toque rápido que fechava o painel trocava o modo junto (testado no motor)
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("interact")) return;
        if (!_playerInside) return;

        AlternarModoTiro();
        GetViewport().SetInputAsHandled(); // Este aperto já foi usado: ninguém mais reage a ele
    }

    private void AlternarModoTiro()
    {
        if (GameState.Instance == null) return;

        // Pede a troca ao estado da partida; o jogador é avisado pelo sinal FlashlightModeChanged
        GameState.Instance.ToggleFlashlightMode();
        bool isManualFire = GameState.Instance.Data.IsManualFire;

        string modo = isManualFire ? "MANUAL (Mira no Mouse)" : "AUTOMÁTICO (Foco na Inércia)";
        GD.Print($"[SUÍTE DE TREINO]: Modo de combate alterado para -> {modo}");
        UpdateModeColor(isManualFire);
    }

    // Feedback visual no terminal (Cyan = Manual, Amarelo = Automático)
    private void UpdateModeColor(bool isManualFire)
    {
        if (_visualRect == null) return;
        _visualRect.Color = isManualFire ? new Color(0f, 1f, 1f) : new Color(1f, 0.8f, 0f);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
        {
            _playerInside = true;
            GD.Print("[SUÍTE DE TREINO]: Pressione 'E' ou 'Espaço' para alternar o modo de iluminação/tiro.");
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
        {
            _playerInside = false;
        }
    }
}
