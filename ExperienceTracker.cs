using Godot;

// Experiência da incursão (spec level-up.md): a biomassa coletada vira XP, e cada nível novo dá 1 ponto de melhoria.
// Fica como nó filho do Player (composição): a recarga da cena traz um nó novo, e a incursão começa do zero.
// Só reage à coleta (sinal BiomassCollected do coletor): não processa a cada passo.
public partial class ExperienceTracker : Node
{
    [Signal] public delegate void ExperienceChangedEventHandler(int level, int xpInLevel, int xpForNext);
    [Signal] public delegate void UpgradePointsChangedEventHandler(int points);

    [Export] public int FirstLevelXp = 10;   // Requisito do nível 1 → 2
    [Export] public int FirstIncrement = 5;  // Acréscimo do requisito no nível 2 → 3
    [Export] public int IncrementGrowth = 1; // Quanto o acréscimo cresce a cada nível

    public int Level { get; private set; } = 1; // A incursão começa no nível 1
    public int XpInLevel { get; private set; }  // XP juntada dentro do nível atual
    public int Points { get; private set; }     // Pontos de melhoria ainda não gastos

    public int XpForNext => XpRequiredFor(Level); // Requisito para sair do nível atual

    public override void _Ready()
    {
        Callable.From(NotifyAll).CallDeferred(); // Estado inicial, depois que o HUD ficou pronto
    }

    // Conectado pelo editor ao sinal BiomassCollected do BiomassCollector: 1 de biomassa = 1 de XP (L3)
    public void OnBiomassCollected(int amount) => AddExperience(amount);

    // Soma a XP e processa todos os níveis que ela cruzar, com 1 ponto por nível; a sobra passa para o nível seguinte
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        XpInLevel += amount;
        int levelsGained = 0;
        while (XpInLevel >= XpForNext)
        {
            XpInLevel -= XpForNext;
            Level++;
            levelsGained++;
        }
        Points += levelsGained;

        EmitExperience();
        if (levelsGained == 0) return;
        EmitPoints();
    }

    // Gasta 1 ponto (escolha no menu de melhorias); devolve false se não houver ponto
    public bool TrySpendPoint()
    {
        if (Points <= 0) return false;

        Points--;
        EmitPoints();
        return true;
    }

    // Requisito para sair do nível: o primeiro mais os acréscimos, que crescem IncrementGrowth por nível
    // (10, 15, 21, 28...: acréscimos de 5, 6, 7...; a soma de uma progressão aritmética).
    // Nunca abaixo de 1: com 0, o laço do AddExperience não terminaria.
    public int XpRequiredFor(int level)
    {
        int steps = level - 1; // Quantos acréscimos já entraram
        int required = FirstLevelXp + steps * FirstIncrement + IncrementGrowth * (steps * (steps - 1) / 2);
        return Mathf.Max(1, required);
    }

    private void NotifyAll()
    {
        EmitExperience();
        EmitPoints();
    }

    private void EmitExperience() => EmitSignal(SignalName.ExperienceChanged, Level, XpInLevel, XpForNext);

    private void EmitPoints() => EmitSignal(SignalName.UpgradePointsChanged, Points);
}
