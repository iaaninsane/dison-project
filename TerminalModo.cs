using Godot;
using System;

public partial class TerminalModo : Area2D
{
    private ColorRect _visualRect;
    private bool _playerInside = false;
    private Player _playerRef;

    public override void _Ready()
    {
        _visualRect = GetNodeOrNull<ColorRect>("ColorRect");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        // Se o jogador estiver na área e pressionar o botão de interação ("interact" ou Espaço/E)
        if (_playerInside && Input.IsActionJustPressed("interact"))
        {
            AlternarModoTiro();
        }
    }

    private void AlternarModoTiro()
    {
        if (_playerRef != null)
        {
            // Inverte o modo de combate no script do Player
            _playerRef.IsManualFire = !_playerRef.IsManualFire;

            string modo = _playerRef.IsManualFire ? "MANUAL (Mira no Mouse)" : "AUTOMÁTICO (Foco na Inércia)";
            GD.Print($"[SUÍTE DE TREINO]: Modo de combate alterado para -> {modo}");

            // Feedback visual no terminal (Cyan = Manual, Amarelo = Automático)
            if (_visualRect != null)
            {
                _visualRect.Color = _playerRef.IsManualFire ? new Color(0f, 1f, 1f) : new Color(1f, 0.8f, 0f);
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            _playerInside = true;
            _playerRef = player;
            GD.Print("[SUÍTE DE TREINO]: Pressione 'E' ou 'Espaço' para alternar o modo de iluminação/tiro.");
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
        {
            _playerInside = false;
            _playerRef = null;
        }
    }
}
