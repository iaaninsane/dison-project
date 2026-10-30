using Godot;

// Estado global da partida (autoload): vive entre as trocas de cena.
// Registrado em Projeto > Configurações do Projeto > Globais com o nome "GameState".
public partial class GameState : Node
{
    [Signal] public delegate void BankChangedEventHandler(int banked);
    [Signal] public delegate void UpgradeLevelChangedEventHandler(string upgradeId, int level);
    [Signal] public delegate void FlashlightModeChangedEventHandler(bool isManualFire);

    public static GameState Instance { get; private set; } // Atalho tipado para o nó /root/GameState

    public RunData Data { get; private set; } = new();

    // Par simétrico: entrou na árvore → vira a instância global; saiu → deixa de ser
    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    // Créditos coletados na incursão. Ainda sem chamador: a fonte de créditos não existe (spec level-up.md, L1)
    public void AddCarriedCredits(int amount)
    {
        if (amount <= 0) return;
        Data.CarriedCredits += amount;
    }

    // Morte: os créditos carregados perdem a parte que não é guardada ("taxa")
    public void ApplyDeathPenalty(float keepFraction)
    {
        Data.CarriedCredits = Mathf.FloorToInt(Data.CarriedCredits * Mathf.Clamp(keepFraction, 0f, 1f));
    }

    // Câmara de partida: deposita no banco todos os créditos carregados
    public void DepositCarried()
    {
        Data.BankedCredits += Data.CarriedCredits;
        Data.CarriedCredits = 0;
        EmitSignal(SignalName.BankChanged, Data.BankedCredits);
    }

    public int GetUpgradeLevel(SuitUpgrade upgrade) => Data.GetUpgradeLevel(upgrade.Id);

    // Pode comprar o próximo nível? (para o painel habilitar ou desabilitar o botão)
    public bool CanBuy(SuitUpgrade upgrade)
    {
        int cost = upgrade.GetCostForNextLevel(GetUpgradeLevel(upgrade));
        return cost >= 0 && Data.BankedCredits >= cost;
    }

    // Tenta comprar o próximo nível com os créditos do banco; devolve true se comprou
    public bool TryBuyUpgrade(SuitUpgrade upgrade)
    {
        if (upgrade == null || string.IsNullOrEmpty(upgrade.Id)) return false;
        if (!CanBuy(upgrade)) return false; // sem saldo ou no nível máximo

        int level = GetUpgradeLevel(upgrade);
        Data.BankedCredits -= upgrade.GetCostForNextLevel(level);
        Data.UpgradeLevels[upgrade.Id] = level + 1;

        EmitSignal(SignalName.BankChanged, Data.BankedCredits);
        EmitSignal(SignalName.UpgradeLevelChanged, upgrade.Id, level + 1);
        return true;
    }

    // TerminalModo: alterna o modo da lanterna (manual ↔ automático) e avisa quem depende dele
    public void ToggleFlashlightMode()
    {
        Data.IsManualFire = !Data.IsManualFire;
        EmitSignal(SignalName.FlashlightModeChanged, Data.IsManualFire);
    }
}
