using UnityEngine;

// Troca a espada entre a mao (RightHandWeapon) e as costas (SwordBack).
// As animacoes Guardar_Espada e Sacar_Espada chamam GuardarEspada()/SacarEspada() por Animation Event.
public class GuardaEspada : MonoBehaviour
{
    public bool guardada;
    public Transform sword, rightHandWeapon, swordBack;
    bool aplicado;

    void Reset() { AcharOssos(); }

    public void AcharOssos()
    {
        sword = Achar("Sword"); rightHandWeapon = Achar("RightHandWeapon"); swordBack = Achar("SwordBack");
    }

    Transform Achar(string nome)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == nome) return t;
        return null;
    }

    public void GuardarEspada() { guardada = true; }   // chamado pelo evento da animacao
    public void SacarEspada() { guardada = false; }    // chamado pelo evento da animacao

    void Update()
    {
        if (guardada == aplicado || sword == null) return;
        sword.SetParent(guardada ? swordBack : rightHandWeapon, false);
        aplicado = guardada;
    }
}
