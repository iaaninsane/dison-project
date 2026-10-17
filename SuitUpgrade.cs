using Godot;

// Definição de uma melhoria do traje: dados editáveis no Inspector, um arquivo .tres por melhoria.
[GlobalClass]
public partial class SuitUpgrade : Resource
{
    public enum TargetStat { SuitMaxIntegrity, MagnetRadius, LightDamagePerSecond } // Valores do jogador que podem melhorar

    [Export] public string Id = "";             // Chave estável usada no save (ex.: "suit_integrity"); não mudar depois
    [Export] public string DisplayName = "";    // Nome mostrado no painel
    [Export] public TargetStat Target;          // Qual valor do jogador esta melhoria aumenta
    [Export] public float ValuePerLevel = 0f;   // Quanto soma a cada nível
    [Export] public int[] Costs = System.Array.Empty<int>(); // Custo de cada nível; o tamanho da lista é o nível máximo

    public int MaxLevel => Costs.Length;

    // Custo para subir do nível atual para o próximo; -1 se já está no máximo
    public int GetCostForNextLevel(int currentLevel) =>
        currentLevel < MaxLevel ? Costs[currentLevel] : -1;
}
