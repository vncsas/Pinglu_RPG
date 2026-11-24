# Generates saida/unity/{Civil2NPC.cs, DemoCivil2.cs, Editor/MontarCivil2.cs} from the Civil 3 package (same structure).
import os, re
R = r"C:/Users/Thiago/Documentos/Pinglu_RPG/Assets/Personagens/Civil3/unity/"
O = r"C:/Users/Thiago/Documentos/rig-civil2/saida/unity/"
os.makedirs(O + "Editor", exist_ok=True)

MOV = ["Parado", "Andar", "Andar_Calmo", "Correr", "Correr_Desesperado", "Carregar_Andar", "Agachado"]
CONV = ["Falar", "Ouvir", "Concordar", "Negar", "Interagir", "Assustado", "Dancar"]
TRAB = ["Beber", "Varrer", "Cortar_Lenha", "Colher", "Plantar", "Regar", "Consertar", "Abrir_Bau", "Pegar_Objeto", "Empurrar", "Tocha", "Lanterna"]
SENT = ["Sentar_Entrar", "Sentado", "Sentado_Falando", "Sentar_Sair"]
DANO = ["Levar_Golpe", "Levar_Golpe_Cabeca", "Levar_Golpe_Forte", "Morrer"]
LOOPS = ["Parado", "Andar", "Andar_Calmo", "Correr", "Correr_Desesperado", "Carregar_Andar", "Agachado", "Falar", "Ouvir", "Assustado", "Dancar",
         "Varrer", "Cortar_Lenha", "Empurrar", "Tocha", "Lanterna", "Sentado", "Sentado_Falando"]
DEPOIS = {"Levar_Golpe": "Parado", "Levar_Golpe_Cabeca": "Parado", "Levar_Golpe_Forte": "Parado", "Concordar": "Parado", "Negar": "Parado",
          "Interagir": "Parado", "Beber": "Parado", "Colher": "Parado", "Plantar": "Parado", "Regar": "Parado", "Consertar": "Parado",
          "Abrir_Bau": "Parado", "Pegar_Objeto": "Parado", "Sentar_Entrar": "Sentado", "Sentar_Sair": "Parado"}
def cs_arr(a): return "{ " + ", ".join(f'"{x}"' for x in a) + " }"

# ---- Editor
s = open(R + "Editor/MontarCivil.cs", encoding="utf8").read()
for a, b in (("MontarCivil", "MontarCivil2"), ("Assets/Personagens/Civil3", "Assets/Personagens/Civil2"), ("civil_rigged", "civil2_rigged"),
             ("Teste_Civil", "Teste_Civil2"), ("civil@", "civil2@"), ('"Civil/', '"Civil2/'), ("Civil > Montar", "Civil2 > Montar"),
             ("[Civil]", "[Civil2]"), ("Civil.controller", "Civil2.controller"), ("Civil_TodosOsOssos", "Civil2_TodosOsOssos"),
             ("DemoCivil", "DemoCivil2"), ("CivilNPC", "Civil2NPC"), ("civil_{p}", "civil2_{p}"), ("Civil_{p}.mat", "Civil2_{p}.mat"),
             ('"Civil_" + p', '"Civil2_" + p'), ("Monta o Civil 3 (NPC)", "Monta o Civil 2 (NPC)"), ('go.name = "Civil3"', 'go.name = "Civil2"')):
    s = s.replace(a, b)
s = re.sub(r"static readonly string\[\] Loops = \{[^}]*\};", "static readonly string[] Loops = " + cs_arr(LOOPS) + ";", s)
s = re.sub(r"var depois = new System\.Collections\.Generic\.Dictionary<string, string> \{.*?\};",
           "var depois = new System.Collections.Generic.Dictionary<string, string> { " + ", ".join('{ "%s", "%s" }' % kv for kv in DEPOIS.items()) + " };", s, flags=re.S)
assert "Civil3" not in s and "civil_" not in s, [m for m in re.findall(r".*(?:Civil3|civil_).*", s)]
open(O + "Editor/MontarCivil2.cs", "w", encoding="utf8").write(s)

# ---- Demo
d = open(R + "DemoCivil.cs", encoding="utf8").read().replace("DemoCivil", "DemoCivil2").replace("Civil 3 (NPC)", "Civil 2 (NPC)").replace("Civil > Montar", "Civil2 > Montar")
d = re.sub(r"public static readonly string\[\] Movimento = \{[^}]*\};", "public static readonly string[] Movimento = " + cs_arr(MOV) + ";", d)
d = re.sub(r"public static readonly string\[\] Conversa = \{[^}]*\};", "public static readonly string[] Conversa = " + cs_arr(CONV) + ";\n    public static readonly string[] Trabalho = " + cs_arr(TRAB) + ";\n    public static readonly string[] Sentar = " + cs_arr(SENT) + ";", d)
d = d.replace("Movimento.Concat(Conversa).Concat(Dano)", "Movimento.Concat(Conversa).Concat(Trabalho).Concat(Sentar).Concat(Dano)")
d = d.replace('{ "Andar", 1.3f }, { "Andar_Calmo", 1.1f }, { "Correr", 3.5f }, { "Correr_Desesperado", 5.0f } }', '{ "Andar", 1.3f }, { "Andar_Calmo", 1.1f }, { "Carregar_Andar", 1.0f }, { "Correr", 3.5f }, { "Correr_Desesperado", 5.0f } }')
d = d.replace('        GUILayout.Label("Levar dano");', '        GUILayout.Label("Trabalho / dia a dia");\n        foreach (var n in Trabalho) if (GUILayout.Button(n)) Tocar(n);\n        GUILayout.Label("Sentar");\n        foreach (var n in Sentar) if (GUILayout.Button(n)) Tocar(n);\n        GUILayout.Label("Levar dano");')
assert "Trabalho) if" in d
open(O + "DemoCivil2.cs", "w", encoding="utf8").write(d)

# ---- NPC
n = open(R + "CivilNPC.cs", encoding="utf8").read().replace("CivilNPC", "Civil2NPC").replace("Civil 3", "Civil 2").replace("Civil > Montar", "Civil2 > Montar").replace("Civil.controller", "Civil2.controller")
extra = '''    public void Beber()             { Tocar("Beber"); }                // bebe de um copo/caneca (volta sozinho pro Parado)
    public void Varrer()            { Tocar("Varrer"); }               // loop, segurando uma vassoura (o prop e do jogo)
    public void CortarLenha()       { Tocar("Cortar_Lenha"); }         // loop, com machado
    public void Colher()            { Tocar("Colher"); }
    public void Plantar()           { Tocar("Plantar"); }
    public void Regar()             { Tocar("Regar"); }
    public void Consertar()         { Tocar("Consertar"); }
    public void AbrirBau()          { Tocar("Abrir_Bau"); }
    public void PegarObjeto()       { Tocar("Pegar_Objeto"); }
    public void Empurrar()          { Tocar("Empurrar"); }             // loop, empurrando carroca / porta
    public void Carregar()          { Tocar("Carregar_Andar"); }       // andando carregando uma caixa / saco
    public void Dancar()            { Tocar("Dancar"); }
    public void Sentar()            { Tocar("Sentar_Entrar"); }        // vai pro Sentado sozinho
    public void Levantar()          { Tocar("Sentar_Sair"); }
    public void SentadoFalando()    { Tocar("Sentado_Falando"); }
    public void Agachar()           { Tocar("Agachado"); }
'''
n = n.replace("    public void Morrer()", extra + "    public void Morrer()")
open(O + "Civil2NPC.cs", "w", encoding="utf8").write(n)
print("ok")
