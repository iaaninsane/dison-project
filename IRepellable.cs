using Godot;

// O que o campo de força empurra (spec force-field.md): os inimigos.
// Separado do IContactHazard (segregação de interfaces): o spawner fere ao encostar, mas anda preso à rota e não é empurrado.
public interface IRepellable
{
    void Repel(Vector2 fromPosition, float impulse);
}
