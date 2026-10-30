using System.Collections.Generic;

// Dados da partida, separados do comportamento: banco de créditos, créditos carregados, níveis das melhorias
// permanentes e modo da lanterna.
// Classe C# comum (não é nó): fácil de salvar em disco depois (converter para arquivo e de volta).
public class RunData
{
    // Créditos: a moeda futura das melhorias permanentes (spec level-up.md, L1). Ainda sem fonte: o saldo fica em 0
    public int BankedCredits;   // Créditos guardados (gastos no terminal de evolução)
    public int CarriedCredits;  // Créditos coletados na incursão atual (vão para o banco na câmara de partida)
    public readonly Dictionary<string, int> UpgradeLevels = new(); // Nível de cada melhoria do banco, pela chave Id
    public bool IsManualFire = true; // Modo da lanterna (TerminalModo): true = manual (mira), false = automático (velocidade)

    public int GetUpgradeLevel(string upgradeId) =>
        UpgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;
}
