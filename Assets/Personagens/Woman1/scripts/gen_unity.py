# Generates saida/unity/{Woman1NPC.cs, DemoWoman1.cs, Editor/MontarWoman1.cs} from the Civil 2 package (same 34 clips).
import os, re
R = r"C:/Users/Thiago/Documentos/rig-civil2/saida/unity/"
O = r"C:/Users/Thiago/Documentos/rig-woman1/saida/unity/"
os.makedirs(O + "Editor", exist_ok=True)
def conv(t):
    for a, b in (("MontarCivil2", "MontarWoman1"), ("DemoCivil2", "DemoWoman1"), ("Civil2NPC", "Woman1NPC"), ("Civil2", "Woman1"), ("civil2", "woman1"),
                 ("Civil 2 (NPC)", "Mulher 1 (NPC)"), ("Monta o Civil 2", "Monta a Mulher 1")):
        t = t.replace(a, b)
    return t
for src, dst in (("Editor/MontarCivil2.cs", "Editor/MontarWoman1.cs"), ("DemoCivil2.cs", "DemoWoman1.cs"), ("Civil2NPC.cs", "Woman1NPC.cs")):
    s = conv(open(R + src, encoding="utf8").read())
    s = s.replace('{ "Body", "Bottom", "Top", "Shoes", "Hair", "Beard", "Moustache" }', '{ "Body", "Top", "Shoes", "Hair" }')
    s = s.replace('{ "Hair", "Beard", "Moustache" }', '{ "Body", "Top", "Shoes", "Hair" }')
    open(O + dst, "w", encoding="utf8").write(s)
t = open(O + "Editor/MontarWoman1.cs", encoding="utf8").read()
assert "Civil" not in t and "civil" not in t, re.findall(r".*[Cc]ivil.*", t)
print("ok")
