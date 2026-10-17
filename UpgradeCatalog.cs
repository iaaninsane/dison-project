using Godot;

// Lista de todas as melhorias do traje: fonte única, usada pelo painel de compra e pelo aplicador.
[GlobalClass]
public partial class UpgradeCatalog : Resource
{
    [Export] public Godot.Collections.Array<SuitUpgrade> Upgrades = new();
}
