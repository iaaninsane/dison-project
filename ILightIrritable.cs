// O que a luz da lanterna irrita peça a peça (spec dyson-wall.md, 2.2): a parede de fronteira (DysonWall).
// Recebe a luz deste passo e a própria lanterna, para perguntar o peso da luz em cada ponto (BeamWeightAt).
// Mesmo espírito do ILightDamageable: a lanterna não precisa conhecer cada tipo de parede.
public interface ILightIrritable
{
    void AddLightIrritation(float amount, LightDamageArea light);
}
