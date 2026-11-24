using UnityEngine;

// Escudo de energia de teste, ligado pelos Animation Events das animacoes de escudo:
//   AtivarEscudo (fim de Escudo_Erguer)  -> uma cupula azul translucida cresce em volta do tanque
//   ImpactoEscudo (golpe de Escudo_Impacto) -> a cupula pisca e uma onda corre pela superficie
//   DesativarEscudo (inicio de Escudo_Baixar) -> a cupula encolhe e some
// A cupula fica centrada no osso ShieldCenter (peito). E so visual de teste: no jogo troque pelo VFX de verdade
// e use os mesmos eventos para ligar/desligar a reducao de dano.
public class EscudoTanque : MonoBehaviour
{
    public Transform centro;
    public Color cor = new Color(0.35f, 0.65f, 1f);
    public float raio = 1.25f;
    public bool ativo;
    Transform cupula, onda; Material mCupula, mOnda; Light luz;
    float alvo, atual, pisca, ondaT = -1f;

    void Reset() { AcharOssos(); }
    public void AcharOssos()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "ShieldCenter") centro = t;
    }

    public void AtivarEscudo() { ativo = true; }
    public void DesativarEscudo() { ativo = false; }
    public void ImpactoEscudo() { pisca = 1f; ondaT = 0f; }

    void Criar()
    {
        var sh = Shader.Find("Sprites/Default");
        cupula = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; Destroy(cupula.GetComponent<Collider>());
        cupula.name = "CupulaEscudo"; mCupula = new Material(sh); cupula.GetComponent<Renderer>().sharedMaterial = mCupula;
        onda = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; Destroy(onda.GetComponent<Collider>());
        onda.name = "OndaEscudo"; mOnda = new Material(sh); onda.GetComponent<Renderer>().sharedMaterial = mOnda;
        luz = cupula.gameObject.AddComponent<Light>(); luz.color = cor; luz.range = raio * 3f;
    }

    void LateUpdate()
    {
        if (!centro) AcharOssos();
        if (!cupula) Criar();
        alvo = ativo ? 1f : 0f;
        atual = Mathf.MoveTowards(atual, alvo, Time.deltaTime * (ativo ? 4f : 3f));
        pisca = Mathf.MoveTowards(pisca, 0f, Time.deltaTime * 3f);
        var pos = centro ? centro.position : transform.position + Vector3.up;
        float e = Mathf.SmoothStep(0f, 1f, atual);
        float respira = 1f + 0.02f * Mathf.Sin(Time.time * 3f);
        cupula.position = pos; cupula.localScale = Vector3.one * (2f * raio * e * respira * (1f + 0.06f * pisca));
        var c = Color.Lerp(cor, Color.white, pisca * 0.6f); c.a = (0.14f + 0.35f * pisca) * e; mCupula.color = c;
        cupula.gameObject.SetActive(e > 0.01f);
        luz.intensity = (1.2f + 6f * pisca) * e;
        if (ondaT >= 0f)
        {
            ondaT += Time.deltaTime / 0.4f;
            onda.gameObject.SetActive(ondaT < 1f && e > 0.01f);
            onda.position = pos; onda.localScale = Vector3.one * 2f * raio * e * (0.6f + 0.45f * ondaT);
            var co = Color.Lerp(cor, Color.white, 0.5f); co.a = 0.35f * (1f - ondaT); mOnda.color = co;
            if (ondaT >= 1f) ondaT = -1f;
        }
        else onda.gameObject.SetActive(false);
    }
}
