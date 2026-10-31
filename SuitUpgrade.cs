using Godot;

// Definição de uma melhoria do traje: dados editáveis no Inspector, um arquivo .tres por melhoria.
// O mesmo recurso serve aos dois catálogos (spec level-up.md): no banco, o custo é em créditos; no level up, em pontos.
[GlobalClass]
public partial class SuitUpgrade : Resource
{
    public enum TargetStat { SuitMaxIntegrity, MagnetRadius, FlashlightPower } // Valores do jogador que podem melhorar
    public enum BonusMode { Flat, Percent } // Como cada nível soma: valor fixo, ou porcentagem do valor-base

    [Export] public string Id = "";             // Chave estável usada no save (ex.: "suit_integrity"); não mudar depois
    [Export] public string DisplayName = "";    // Nome mostrado no painel e na carta
    [Export(PropertyHint.MultilineText)] public string Description = ""; // Texto da carta do level up
    [Export] public TargetStat Target;          // Qual valor do jogador esta melhoria aumenta
    [Export] public BonusMode ValueMode = BonusMode.Flat; // Soma fixa ou porcentagem do valor-base
    [Export] public float ValuePerLevel = 0f;   // Quanto soma a cada nível (no modo Percent, em %: 25 = +25% do valor-base)
    [Export] public int[] Costs = System.Array.Empty<int>(); // Custo de cada nível; o tamanho da lista é o nível máximo

    public int MaxLevel => Costs.Length;

    // Custo para subir do nível atual para o próximo; -1 se já está no máximo
    public int GetCostForNextLevel(int currentLevel) =>
        currentLevel < MaxLevel ? Costs[currentLevel] : -1;
}
