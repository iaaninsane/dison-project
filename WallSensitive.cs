using Godot;
using System;

public partial class WallSensitive : StaticBody2D
{
    [Export] public float MaxIrritation = 15f;
    [Export] public float RecoveryRate = 15f; // Quão rápido a parede se acalma por segundo
    [Export] public float EjectionForce = 800f; // Força do empurrão físico

    private float _currentIrritation = 0f;
    private ColorRect _visualRect; // Referência ao ColorRect para mudar de cor
    private Color _normalColor = new Color(1f, 1f, 1f); // Branco padrão do labirinto
    private Color _irritatedColor = new Color(2f, 0.1f, 0.1f); // Vermelho neon pulsante (Glow)

    public override void _Ready() {
        // Obtém o retângulo visual da parede para darmos feedback de cor
        _visualRect = GetNodeOrNull<ColorRect>("ColorRect");

                if (_visualRect == null)
        {
            GD.PrintErr($"[SISTEMA - {Name}]: ERRO CRÍTICO! O script não encontrou o nó 'ColorRect'. " +
                       "Verifique se o nó filho na árvore de cenas está escrito exatamente como 'ColorRect' " +
                       "(confira se não ficou salvo como 'ColorRetc' ou com espaços).");
        }
        else
        {
            GD.Print($"[SISTEMA - {Name}]: SUCESSO! Nó visual 'ColorRect' encontrado e referenciado.");
        }
    }

    public override void _PhysicsProcess(double delta) {

        if (_currentIrritation > 0f) {
            // Decai a irritação gradualmente se não estiver recebendo luz
            _currentIrritation = Mathf.Max(0f, _currentIrritation - (RecoveryRate * (float)delta));
           UpdateVisuals();
    
        }
    }

    // Método chamado dinamicamente pela LightDamageArea da lanterna
    public void AddIrritation(float amount, Vector2 playerPosition) {
        _currentIrritation = Mathf.Min(MaxIrritation, _currentIrritation + amount);
        
        // 2. DIAGNÓSTICO DO ACÚMULO DE VALOR
        GD.Print($"[SISTEMA - {Name}]: Acumulando irritação! Valor atual: {_currentIrritation}/{MaxIrritation} (Adicionado: {amount})");
        
        UpdateVisuals();

        if (_currentIrritation >= MaxIrritation) {
            TriggerEjection(playerPosition);
        }
    }

    private void UpdateVisuals() {

        if (_visualRect != null) {
            // Interpola a cor com base no nível atual de estresse (0.0 a 1.0)
            float ratio = _currentIrritation / MaxIrritation;
            _visualRect.Modulate = _normalColor.Lerp(_irritatedColor, ratio);
        }
    }

    private void TriggerEjection(Vector2 playerPosition) {
        // Encontra o Player ativo no grupo
        var player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player != null && !player.IsEjected) {

            // 1. Calcula o centro real da parede (Posição + Metade da dimensão)
            Vector2 wallCenter = GlobalPosition;
            if (_visualRect != null) {
                wallCenter = _visualRect.GlobalPosition + (_visualRect.Size / 2f);
            }
                        
            // Calcula o vetor de expulsão: do centro da parede apontando para o jogador
            Vector2 pullDirection = (wallCenter - playerPosition).Normalized();
            
            // Zera a irritação local após o espasmo de ejeção
            _currentIrritation = 0f;
            
            // Aplica o empurrão violento no Player
            player.ApplyEjection(pullDirection * EjectionForce);
        }
    }
}
