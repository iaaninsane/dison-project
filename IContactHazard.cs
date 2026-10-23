using Godot;

// O que fere o traje ao encostar (spec mobile-spawners.md): inimigos e spawners.
// O PlayerHurtbox sente qualquer coisa que cumpra este contrato, sem conhecer cada tipo.
public interface IContactHazard
{
    bool IsDead { get; }            // Morto (ou guardado num estoque) não fere
    Vector2 GlobalPosition { get; } // De onde vem o golpe (o empurrão afasta o jogador daqui); todo Node2D já tem
}
