using UnityEngine;

// Animal de fazenda (ovelha, cabra, porco, cavalo): os modelos não têm esqueleto, então a "animação" é feita no corpo todo:
// anda devagar pelo pasto com balanço de passo (sobe e desce, inclina de lado), para para respirar e abaixa a cabeça para pastar.
// Só enfeita o mapa: não é rede, não ataca e não tem vida. Longe da câmera fica desligado (não pesa).
// O objeto deve ter UM filho (o modelo), já virado com a frente (cabeça) para o +Z do objeto.
public class AnimalDeFazenda : MonoBehaviour
{
    public float raio = 14f;
    public float velocidade = 0.9f;
    public float comprimento = 1.3f;          // comprimento do corpo (m), para o ritmo do passo
    public float amplitudePasso = 1f;
    public float distanciaParaAnimar = 110f;

    private enum Estado { Parado, Andando, Pastando }
    private Estado estado = Estado.Parado;
    private float fimDoEstado, fase, aba;
    private Vector3 casa, destino;
    private Transform modelo;
    private Vector3 posBase; private Quaternion rotBase;
    private Terrain terreno;
    private Camera cam;
    private bool ativo = true;
    private float proximaChecagem, proximaVista;
    private float inclinacaoPasto;

    private void Start()
    {
        casa = transform.position; destino = casa;
        terreno = Terrain.activeTerrain;
        if (transform.childCount > 0) { modelo = transform.GetChild(0); posBase = modelo.localPosition; rotBase = modelo.localRotation; }
        fase = Random.value * 6.28f;
        fimDoEstado = Time.time + Random.Range(0.5f, 6f);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private void Update()
    {
        if (modelo == null) return;
        if (Time.time >= proximaChecagem)
        {
            proximaChecagem = Time.time + 0.5f;
            if (cam == null) cam = Camera.main;
            ativo = cam == null || (cam.transform.position - transform.position).sqrMagnitude < distanciaParaAnimar * distanciaParaAnimar;
        }
        if (!ativo) return;
        float dt = Time.deltaTime;

        if (Time.time >= fimDoEstado) Sortear();

        float bob = 0f, rolar = 0f, arfar = 0f, alvoPasto = 0f;
        if (estado == Estado.Andando)
        {
            Vector3 para = destino - transform.position; para.y = 0f;
            if (para.magnitude < 0.5f) { estado = Estado.Parado; fimDoEstado = Time.time + Random.Range(2f, 6f); }
            else
            {
                Vector3 dir = para.normalized;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 110f * dt);
                float alinhado = Mathf.Clamp01(Vector3.Dot(transform.forward, dir));
                if (Time.time >= proximaVista) { proximaVista = Time.time + 0.3f; if (Bloqueado()) { estado = Estado.Parado; fimDoEstado = Time.time + Random.Range(1f, 3f); } }
                Vector3 np = transform.position + transform.forward * velocidade * alinhado * dt;
                np.y = AlturaDoChao(np);
                transform.position = np;
                float passo = Mathf.Max(0.3f, comprimento * 0.55f);
                fase += dt * (velocidade / passo) * 6.283f;
                bob = Mathf.Abs(Mathf.Sin(fase)) * 0.035f * comprimento * amplitudePasso;
                rolar = Mathf.Sin(fase) * 3.2f * amplitudePasso;
                arfar = Mathf.Sin(fase * 2f + 1.2f) * 1.6f;
            }
        }
        else
        {
            fase += dt * 1.6f;
            bob = Mathf.Sin(fase) * 0.004f * comprimento;                         // respiração
            if (estado == Estado.Pastando) alvoPasto = 13f;
        }
        inclinacaoPasto = Mathf.Lerp(inclinacaoPasto, alvoPasto, 1.5f * dt);
        modelo.localPosition = posBase + Vector3.up * bob;
        modelo.localRotation = Quaternion.Euler(arfar + inclinacaoPasto, 0f, rolar) * rotBase;
    }

    private void Sortear()
    {
        float r = Random.value;
        if (r < 0.5f)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 c = Random.insideUnitCircle * raio;
                Vector3 d = casa + new Vector3(c.x, 0f, c.y);
                if (PontoBom(d)) { destino = d; estado = Estado.Andando; fimDoEstado = Time.time + 40f; return; }
            }
            estado = Estado.Parado; fimDoEstado = Time.time + 3f;
        }
        else if (r < 0.8f) { estado = Estado.Pastando; fimDoEstado = Time.time + Random.Range(4f, 10f); }
        else { estado = Estado.Parado; fimDoEstado = Time.time + Random.Range(2f, 6f); }
    }

    private float AlturaDoChao(Vector3 p)
    {
        return terreno != null ? terreno.SampleHeight(p) + terreno.transform.position.y : p.y;
    }

    private bool PontoBom(Vector3 p)
    {
        if (terreno == null) return true;
        Vector3 o = terreno.transform.position, s = terreno.terrainData.size;
        float nx = (p.x - o.x) / s.x, nz = (p.z - o.z) / s.z;
        if (nx < 0.01f || nz < 0.01f || nx > 0.99f || nz > 0.99f) return false;
        if (terreno.terrainData.GetSteepness(nx, nz) > 22f) return false;
        return terreno.SampleHeight(p) > 0.8f;     // não entra na água
    }

    // obstáculo à frente (casa, cerca, tronco): para e escolhe outro caminho
    private bool Bloqueado()
    {
        Vector3 origem = transform.position + Vector3.up * 0.6f;
        RaycastHit[] hits = Physics.SphereCastAll(origem, 0.35f, transform.forward, 1.6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponent<TerrainCollider>() != null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }
}
