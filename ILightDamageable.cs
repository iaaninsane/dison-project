// O que a luz da lanterna fere (spec mobile-spawners.md): inimigos e spawners.
// A LightDamageArea fere qualquer coisa que cumpra este contrato, sem conhecer cada tipo.
public interface ILightDamageable
{
    void TakeDamage(float amount);
}
