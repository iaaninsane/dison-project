using Godot;
using System;
using System.Collections.Generic;

public partial class WallSensitive : StaticBody2D
{
    [Export] public float MaxIrritation = 15f;
    [Export] public float RecoveryRate = 15f; // Quão rápido a parede se acalma por segundo
    [Export] public float EjectionForce = 800f; // Força do empurrão físico
    [Export] public float EjectionPathWidth = 16f;     // Largura do "túnel" do puxão (o jogador tem 10 px; em diagonal ocupa ~14)
    [Export] public float EjectionPathOvershoot = 60f; // Quanto o túnel passa do centro (saída pela face de trás)

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

        // A Godot liga o _PhysicsProcess antes do _Ready; desligar aqui faz a parede calma não custar nada
        SetPhysicsProcess(false); // Calma: dorme até a luz chegar (AddIrritation acorda)
    }

    // Só roda enquanto a parede está irritada: decai a irritação e, ao chegar a zero, volta a dormir
    public override void _PhysicsProcess(double delta) {
        _currentIrritation = Mathf.Max(0f, _currentIrritation - (RecoveryRate * (float)delta));
        UpdateVisuals();

        if (_currentIrritation <= 0f) SetPhysicsProcess(false); // Calma de novo
    }

    // Método chamado dinamicamente pela LightDamageArea da lanterna
    public void AddIrritation(float amount, Vector2 playerPosition) {
        _currentIrritation = Mathf.Min(MaxIrritation, _currentIrritation + amount);
        UpdateVisuals();
        SetPhysicsProcess(true); // Acorda para se acalmar aos poucos quando a luz sair

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

            // Vetor de sucção: do jogador apontando para o centro da parede (puxa)
            Vector2 pullDirection = (GetWallCenter() - playerPosition).Normalized();

            // Zera a irritação local após o espasmo de ejeção
            _currentIrritation = 0f;

            // Aplica o puxão violento no Player: ele atravessa esta parede e as que estão no caminho até ela
            player.ApplyEjection(pullDirection * EjectionForce, GetWallsOnPath(playerPosition, pullDirection));
        }
    }

    // Centro geométrico real do retângulo visual, em coordenadas globais.
    // Size / 2 é o centro no espaço local do ColorRect; a transformação global aplica posição,
    // rotação e escala dele e de todos os pais (vale para pivô no canto ou no centro).
    private Vector2 GetWallCenter() {
        if (_visualRect == null) return GlobalPosition;
        return _visualRect.GetGlobalTransform() * (_visualRect.Size / 2f);
    }

    // Paredes reativas que o puxão cruza: um "túnel" do jogador até um pouco depois do centro desta parede.
    // Consulta ao espaço físico: só é segura durante o passo de física (a ejeção nasce no _PhysicsProcess da LightDamageArea).
    private List<PhysicsBody2D> GetWallsOnPath(Vector2 from, Vector2 direction) {
        Vector2 to = GetWallCenter() + direction * EjectionPathOvershoot;
        var query = new PhysicsShapeQueryParameters2D {
            Shape = new RectangleShape2D { Size = new Vector2(from.DistanceTo(to), EjectionPathWidth) },
            Transform = new Transform2D(direction.Angle(), (from + to) / 2f), // Retângulo girado ao longo do caminho
            CollisionMask = CollisionLayer, // Só a camada das paredes
        };

        var walls = new List<PhysicsBody2D> { this }; // A parede que disparou sempre entra
        foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query)) {
            if (hit["collider"].As<GodotObject>() is PhysicsBody2D body && body != this && body.IsInGroup("Walls")) {
                walls.Add(body);
            }
        }
        return walls;
    }
}
