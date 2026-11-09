using UnityEngine;

// "Quem decide" o que o personagem faz. O PlayerLocomotion (e o voo do Tanque) não lê mais o teclado
// direto: perguntam para esta camada. Assim o MESMO corpo, animações, combos e habilidades podem ser
// controlados por uma pessoa (EntradaHumana) ou por um companheiro CPU (EntradaCPU, fase 2 do plano).
public abstract class EntradaDoPersonagem : MonoBehaviour
{
    // movimento: x = esquerda/direita, y = frente/trás (relativo à câmera, como o teclado)
    public abstract Vector2 Mover { get; }
    public abstract bool Correr { get; }
    public abstract bool PularApertou { get; }
    public abstract bool RolarApertou { get; }
    public abstract bool AtaqueApertou { get; }
    public abstract bool DefenderSegurando { get; }
    public abstract bool GuardarSacarApertou { get; }
    // voo do Tanque
    public abstract bool VoarApertou { get; }
    public abstract bool SubirSegurando { get; }
    public abstract bool DescerSegurando { get; }

    // CPUs andam em direções do MUNDO (não existe câmera para elas): se TemDirecaoDeMundo, ela vale no lugar de Mover.
    // O tamanho do vetor (0 a 1) é a intensidade (0,5 = andar devagar).
    public virtual bool TemDirecaoDeMundo { get { return false; } }
    public virtual Vector3 DirecaoDeMundo { get { return Vector3.zero; } }
    // para onde olha ao atacar/defender (CPU: na direção do inimigo). Humano: usa a câmera.
    public virtual bool TemFrenteDeMira { get { return false; } }
    public virtual Vector3 FrenteDeMira { get { return Vector3.forward; } }
}

// Teclado e mouse: exatamente o que o jogo já fazia antes.
public class EntradaHumana : EntradaDoPersonagem
{
    public KeyCode teclaGuardarSacar = KeyCode.H;
    public KeyCode teclaVoar = KeyCode.F, teclaSubir = KeyCode.Space, teclaDescer = KeyCode.C;

    public override Vector2 Mover { get { return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")); } }
    public override bool Correr { get { return Input.GetKey(KeyCode.LeftShift); } }
    public override bool PularApertou { get { return Input.GetKeyDown(KeyCode.Space); } }
    public override bool RolarApertou { get { return Input.GetKeyDown(KeyCode.LeftControl); } }
    public override bool AtaqueApertou { get { return Input.GetMouseButtonDown(0); } }
    public override bool DefenderSegurando { get { return Input.GetMouseButton(1) || Input.GetKey(KeyCode.Q); } }
    public override bool GuardarSacarApertou { get { return Input.GetKeyDown(teclaGuardarSacar); } }
    public override bool VoarApertou { get { return Input.GetKeyDown(teclaVoar); } }
    public override bool SubirSegurando { get { return Input.GetKey(teclaSubir); } }
    public override bool DescerSegurando { get { return Input.GetKey(teclaDescer); } }
}
