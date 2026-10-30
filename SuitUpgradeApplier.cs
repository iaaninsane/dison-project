using Godot;
using System;

// Aplica as melhorias nos componentes do jogador, juntando as duas fontes (spec level-up.md): o banco (melhorias
// permanentes, níveis no GameState) e a incursão (level up, níveis no IncursionUpgrades).
// Recalcula tudo do zero a cada mudança: valor-base × (1 + soma das %) + soma fixa, por alvo.
// Os componentes (SuitIntegrity, BiomassCollector) não sabem que melhorias existem.
public partial class SuitUpgradeApplier : Node
{
    [Export] public UpgradeCatalog Catalog;      // Catálogo do banco (o mesmo do painel de compra)
    [Export] public IncursionUpgrades Incursion; // Níveis e catálogo do level up (ligado no Inspector)
    [Export] public SuitIntegrity Integrity;     // Alvo: integridade máxima
    [Export] public BiomassCollector Collector;  // Alvo: raio do ímã

    private int _baseMaxIntegrity;   // Valores da cena, antes das melhorias
    private float _baseMagnetRadius;

    public override void _Ready()
    {
        if (Integrity != null) _baseMaxIntegrity = Integrity.MaxIntegrity;
        if (Collector != null) _baseMagnetRadius = Collector.MagnetRadius;
        Callable.From(ApplyAll).CallDeferred(); // Depois que todos os componentes ficaram prontos

        if (Incursion != null) Incursion.UpgradeLevelChanged += OnUpgradeLevelChanged;
        if (GameState.Instance == null) return;
        GameState.Instance.UpgradeLevelChanged += OnUpgradeLevelChanged;
    }

    // Par simétrico: o aplicador é da cena, o GameState é global
    public override void _ExitTree()
    {
        if (Incursion != null) Incursion.UpgradeLevelChanged -= OnUpgradeLevelChanged;
        if (GameState.Instance == null) return;
        GameState.Instance.UpgradeLevelChanged -= OnUpgradeLevelChanged;
    }

    // Prévia de uma carta do level up: o valor do alvo agora, com um nível a mais e o valor-base (sem melhorias)
    public (float before, float after, float baseValue) PreviewIncursionLevelUp(SuitUpgrade upgrade)
    {
        StatBonuses bonuses = SumBonuses();
        float baseValue = GetBaseValue(upgrade.Target);
        float before = bonuses.ApplyTo(upgrade.Target, baseValue);
        bonuses.Add(upgrade, 1);
        return (before, bonuses.ApplyTo(upgrade.Target, baseValue), baseValue);
    }

    private void OnUpgradeLevelChanged(string upgradeId, int level) => ApplyAll();

    private void ApplyAll()
    {
        StatBonuses bonuses = SumBonuses();
        Integrity?.SetMaxIntegrity(Mathf.RoundToInt(bonuses.ApplyTo(SuitUpgrade.TargetStat.SuitMaxIntegrity, _baseMaxIntegrity)));
        if (Collector != null) Collector.MagnetRadius = bonuses.ApplyTo(SuitUpgrade.TargetStat.MagnetRadius, _baseMagnetRadius);
    }

    // As duas fontes somadas por alvo
    private StatBonuses SumBonuses()
    {
        var bonuses = new StatBonuses();
        if (GameState.Instance != null) AddSource(bonuses, Catalog, GameState.Instance.GetUpgradeLevel);
        if (Incursion != null) AddSource(bonuses, Incursion.Catalog, Incursion.GetLevel);
        return bonuses;
    }

    private static void AddSource(StatBonuses bonuses, UpgradeCatalog catalog, Func<SuitUpgrade, int> levelOf)
    {
        if (catalog == null) return;
        foreach (SuitUpgrade upgrade in catalog.Upgrades)
            if (upgrade != null) bonuses.Add(upgrade, levelOf(upgrade));
    }

    private float GetBaseValue(SuitUpgrade.TargetStat target) => target switch
    {
        SuitUpgrade.TargetStat.SuitMaxIntegrity => _baseMaxIntegrity,
        SuitUpgrade.TargetStat.MagnetRadius => _baseMagnetRadius,
        _ => 0f,
    };

    // Soma dos bônus de cada alvo: os fixos e os em porcentagem do valor-base
    private sealed class StatBonuses
    {
        private static readonly int TargetCount = Enum.GetValues<SuitUpgrade.TargetStat>().Length;
        private readonly float[] _flat = new float[TargetCount];
        private readonly float[] _percent = new float[TargetCount];

        public void Add(SuitUpgrade upgrade, int levels)
        {
            float bonus = levels * upgrade.ValuePerLevel;
            if (upgrade.ValueMode == SuitUpgrade.BonusMode.Percent) _percent[(int)upgrade.Target] += bonus;
            else _flat[(int)upgrade.Target] += bonus;
        }

        public float ApplyTo(SuitUpgrade.TargetStat target, float baseValue) =>
            baseValue * (1f + _percent[(int)target] / 100f) + _flat[(int)target];
    }
}
