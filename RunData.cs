using System.Collections.Generic;

// Dados da partida, separados do comportamento: banco, biomassa carregada, níveis das melhorias e modo da lanterna.
// Classe C# comum (não é nó): fácil de salvar em disco depois (converter para arquivo e de volta).
public class RunData
{
    public int BankedBiomass;   // Biomassa guardada (gasta no terminal de evolução)
    public int CarriedBiomass;  // Biomassa coletada na incursão atual (mostrada no HUD)
    public readonly Dictionary<string, int> UpgradeLevels = new(); // Nível de cada melhoria, pela chave Id
    public bool IsManualFire = true; // Modo da lanterna (TerminalModo): true = manual (mira), false = automático (velocidade)

    public int GetUpgradeLevel(string upgradeId) =>
        UpgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;
}
