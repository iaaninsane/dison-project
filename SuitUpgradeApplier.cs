using Godot;

// Aplica as melhorias compradas nos componentes do jogador: ao entrar na sala e a cada compra.
// Os componentes (SuitIntegrity, BiomassCollector) não sabem que melhorias existem.
public partial class SuitUpgradeApplier : Node
{
    [Export] public UpgradeCatalog Catalog;      // Mesma lista usada pelo painel
    [Export] public SuitIntegrity Integrity;     // Alvo: integridade máxima
    [Export] public BiomassCollector Collector;  // Alvo: raio do ímã

    private int _baseMaxIntegrity;   // Valores da cena, antes das melhorias
    private float _baseMagnetRadius;

    public override void _Ready()
    {
        if (Integrity != null) _baseMaxIntegrity = Integrity.MaxIntegrity;
        if (Collector != null) _baseMagnetRadius = Collector.MagnetRadius;
        Callable.From(ApplyAll).CallDeferred(); // Depois que todos os componentes ficaram prontos

        if (GameState.Instance == null) return;
        GameState.Instance.UpgradeLevelChanged += OnUpgradeLevelChanged;
    }

    // Par simétrico: o aplicador é da cena, o GameState é global
    public override void _ExitTree()
    {
        if (GameState.Instance == null) return;
        GameState.Instance.UpgradeLevelChanged -= OnUpgradeLevelChanged;
    }

    private void OnUpgradeLevelChanged(string upgradeId, int level) => ApplyAll();

    // Recalcula tudo a partir dos valores base: base + soma (nível × valor por nível) de cada alvo
    private void ApplyAll()
    {
        if (Catalog == null || GameState.Instance == null) return;

        float integrityBonus = 0f;
        float magnetBonus = 0f;

        foreach (SuitUpgrade upgrade in Catalog.Upgrades)
        {
            if (upgrade == null) continue;
            float bonus = GameState.Instance.GetUpgradeLevel(upgrade) * upgrade.ValuePerLevel;

            switch (upgrade.Target)
            {
                case SuitUpgrade.TargetStat.SuitMaxIntegrity: integrityBonus += bonus; break;
                case SuitUpgrade.TargetStat.MagnetRadius: magnetBonus += bonus; break;
            }
        }

        Integrity?.SetMaxIntegrity(_baseMaxIntegrity + Mathf.RoundToInt(integrityBonus));
        if (Collector != null) Collector.MagnetRadius = _baseMagnetRadius + magnetBonus;
    }
}
