using Godot;

// Estado global da partida (autoload): vive entre as trocas de cena.
// Registrado em Projeto > Configurações do Projeto > Globais com o nome "GameState".
public partial class GameState : Node
{
    [Signal] public delegate void BankChangedEventHandler(int banked);
    [Signal] public delegate void UpgradeLevelChangedEventHandler(string upgradeId, int level);

    public static GameState Instance { get; private set; } // Atalho tipado para o nó /root/GameState

    public RunData Data { get; private set; } = new();

    // Par simétrico: entrou na árvore → vira a instância global; saiu → deixa de ser
    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    // Biomassa coletada na incursão (chamado pelo BiomassCollector)
    public void AddCarried(int amount)
    {
        if (amount <= 0) return;
        Data.CarriedBiomass += amount;
    }

    // Morte: a biomassa carregada perde a parte que não é guardada ("taxa")
    public void ApplyDeathPenalty(float keepFraction)
    {
        int before = Data.CarriedBiomass;
        Data.CarriedBiomass = Mathf.FloorToInt(before * Mathf.Clamp(keepFraction, 0f, 1f));
        GD.Print($"[GameState] Morte: carregada {before} -> {Data.CarriedBiomass}"); // TEMPORÁRIO (sai no passo 5)
    }

    // Câmara inicial: deposita no banco tudo o que está carregado
    public void DepositCarried()
    {
        Data.BankedBiomass += Data.CarriedBiomass;
        Data.CarriedBiomass = 0;
        GD.Print($"[GameState] Depósito: banco = {Data.BankedBiomass}"); // TEMPORÁRIO (sai no passo 5)
        EmitSignal(SignalName.BankChanged, Data.BankedBiomass);
    }

    public int GetUpgradeLevel(SuitUpgrade upgrade) => Data.GetUpgradeLevel(upgrade.Id);

    // Pode comprar o próximo nível? (para o painel habilitar ou desabilitar o botão)
    public bool CanBuy(SuitUpgrade upgrade)
    {
        int cost = upgrade.GetCostForNextLevel(GetUpgradeLevel(upgrade));
        return cost >= 0 && Data.BankedBiomass >= cost;
    }

    // Tenta comprar o próximo nível; devolve true se comprou
    public bool TryBuyUpgrade(SuitUpgrade upgrade)
    {
        if (upgrade == null || string.IsNullOrEmpty(upgrade.Id)) return false;
        if (!CanBuy(upgrade)) return false; // sem saldo ou no nível máximo

        int level = GetUpgradeLevel(upgrade);
        Data.BankedBiomass -= upgrade.GetCostForNextLevel(level);
        Data.UpgradeLevels[upgrade.Id] = level + 1;

        EmitSignal(SignalName.BankChanged, Data.BankedBiomass);
        EmitSignal(SignalName.UpgradeLevelChanged, upgrade.Id, level + 1);
        return true;
    }
}
