using UnityEngine;

// Entrada dos companheiros CPU: o CerebroCompanheiro preenche estes campos e o PlayerLocomotion lê
// exatamente como leria o teclado. Os "apertou" valem por UM quadro inteiro (apagam no LateUpdate).
public class EntradaCPU : EntradaDoPersonagem
{
    public Vector3 direcaoDeMundo;          // 0 a 1 de intensidade
    public Vector3 frenteDeMira; public bool temMira;
    public bool correr, defender, subir, descer;
    private bool pular, rolar, atacar, guardarSacar, voar;

    public void Pular() { pular = true; }
    public void Rolar() { rolar = true; }
    public void Atacar() { atacar = true; }
    public void GuardarSacar() { guardarSacar = true; }
    public void Voar() { voar = true; }

    private void LateUpdate() { pular = rolar = atacar = guardarSacar = voar = false; }

    public override Vector2 Mover { get { return Vector2.zero; } }
    public override bool Correr { get { return correr; } }
    public override bool PularApertou { get { return pular; } }
    public override bool RolarApertou { get { return rolar; } }
    public override bool AtaqueApertou { get { return atacar; } }
    public override bool DefenderSegurando { get { return defender; } }
    public override bool GuardarSacarApertou { get { return guardarSacar; } }
    public override bool VoarApertou { get { return voar; } }
    public override bool SubirSegurando { get { return subir; } }
    public override bool DescerSegurando { get { return descer; } }
    public override bool TemDirecaoDeMundo { get { return true; } }
    public override Vector3 DirecaoDeMundo { get { return direcaoDeMundo; } }
    public override bool TemFrenteDeMira { get { return temMira; } }
    public override Vector3 FrenteDeMira { get { return frenteDeMira; } }
}
