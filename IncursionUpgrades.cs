using Godot;
using System.Collections.Generic;

// Melhorias da incursão (spec level-up.md): os níveis dos atributos comprados com os pontos de nível e as cartas da
// escolha atual. Fica como nó filho do Player: a recarga da cena traz um nó novo, e tudo volta ao início.
// Não aplica nada: avisa a mudança de nível (UpgradeLevelChanged), e o SuitUpgradeApplier aplica.
public partial class IncursionUpgrades : Node
{
    [Signal] public delegate void UpgradeLevelChangedEventHandler(string upgradeId, int level);

    [Export] public UpgradeCatalog Catalog;       // Catálogo do level up (upgrades/levelup_catalog.tres)
    [Export] public ExperienceTracker Experience; // De onde saem os pontos (ligado no Inspector)
    [Export] public int OptionsPerChoice = 3;     // Cartas por escolha

    private readonly Dictionary<string, int> _levels = new(); // Nível de cada melhoria na incursão, pela chave Id
    private readonly List<SuitUpgrade> _offers = new();       // Cartas da escolha atual; vazia = sortear de novo

    public int Points => Experience?.Points ?? 0;

    // Guardado de 0 (sem melhoria) ao nível máximo; na tela, aparece somado de 1
    public int GetLevel(SuitUpgrade upgrade)
    {
        if (string.IsNullOrEmpty(upgrade.Id)) return 0;
        return _levels.TryGetValue(upgrade.Id, out int level) ? level : 0;
    }

    // As cartas da escolha atual: sorteadas na primeira vez e guardadas até gastar o ponto (L8)
    public IReadOnlyList<SuitUpgrade> GetOffers()
    {
        if (_offers.Count == 0) DrawOffers();
        return _offers;
    }

    // Escolhe uma das cartas: gasta 1 ponto, sobe o nível e avisa; a próxima escolha terá um sorteio novo
    public bool TryChoose(SuitUpgrade upgrade)
    {
        if (!_offers.Contains(upgrade)) return false;                        // Só vale carta da escolha atual
        if (Experience == null || !Experience.TrySpendPoint()) return false; // Sem ponto

        int level = GetLevel(upgrade) + 1;
        _levels[upgrade.Id] = level;
        _offers.Clear();
        EmitSignal(SignalName.UpgradeLevelChanged, upgrade.Id, level);
        return true;
    }

    // Embaralha as elegíveis e fica com as primeiras (Fisher–Yates): cartas distintas, todas com a mesma chance
    private void DrawOffers()
    {
        if (Catalog == null) return;

        var eligible = new List<SuitUpgrade>();
        foreach (SuitUpgrade upgrade in Catalog.Upgrades)
            if (IsEligible(upgrade)) eligible.Add(upgrade);

        for (int i = eligible.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
        }

        _offers.AddRange(eligible.GetRange(0, Mathf.Min(OptionsPerChoice, eligible.Count)));
    }

    // Elegível: abaixo do nível máximo (a lista de custos dá o máximo, como no banco)
    private bool IsEligible(SuitUpgrade upgrade) =>
        upgrade != null && !string.IsNullOrEmpty(upgrade.Id) && GetLevel(upgrade) < upgrade.MaxLevel;
}
