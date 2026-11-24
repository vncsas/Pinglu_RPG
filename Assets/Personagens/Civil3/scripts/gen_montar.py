# Generates saida/unity/Editor/MontarCivil.cs from the golem's MontarGolem.cs (same structure, 7 URP materials).
g = open(r"C:/Users/Thiago/Documentos/Pinglu_RPG/Assets/Personagens/GolemCemiterio/unity/Editor/MontarGolem.cs", encoding="utf8").read()
s = (g.replace("MontarGolem", "MontarCivil").replace("GolemCemiterio", "Civil3").replace("golem_rigged", "civil_rigged")
      .replace("Teste_Golem", "Teste_Civil").replace("golem@", "civil@").replace('"Golem/', '"Civil/').replace("Golem > Montar", "Civil > Montar")
      .replace("[Golem]", "[Civil]").replace("Golem.controller", "Civil.controller").replace("Golem_TodosOsOssos", "Civil_TodosOsOssos")
      .replace("DemoGolem", "DemoCivil").replace("Monta o Golem do Cemiterio (mini-boss)", "Monta o Civil 3 (NPC)")
      .replace("(loop + eventos de impacto, pisada e batida no peito)", "(loops e volta sozinha pro Parado)"))
s = s.replace('{ "Parado", "Andar", "Correr", "Postura_Luta", "Atordoado" }',
              '{ "Parado", "Andar", "Andar_Calmo", "Correr", "Correr_Desesperado", "Falar", "Ouvir", "Assustado" }')
a = s.index("    static void Texturas()"); b = s.index("    static void ImportarClipes")
new = '''    static readonly string[] Partes = { "Body", "Bottom", "Top", "Shoes", "Hair", "Beard", "Moustache" };
    static readonly string[] ComAlfa = { "Hair", "Beard", "Moustache" };

    static void Texturas()
    {
        foreach (var p in Partes)
        {
            var n = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/texturas_unity/civil_{p}_Normal.png");
            n.textureType = TextureImporterType.NormalMap; n.SaveAndReimport();
            var s = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/texturas_unity/civil_{p}_Specular.png");
            s.sRGBTexture = false; s.alphaSource = TextureImporterAlphaSource.FromInput; s.SaveAndReimport();
            var c = (TextureImporter)AssetImporter.GetAtPath($"{Raiz}/texturas_unity/civil_{p}_BaseColor.png");
            c.alphaIsTransparency = ComAlfa.Contains(p); c.SaveAndReimport();
        }
    }

    static Avatar Personagem()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(Modelo);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        var limpo = mi.humanDescription; limpo.human = new HumanBone[0]; limpo.skeleton = new SkeletonBone[0];
        mi.humanDescription = limpo;
        mi.SaveAndReimport();
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var p in Partes)
        {
            var mpath = $"{Raiz}/Civil_{p}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mpath);
            if (!mat) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mpath); }
            Texture2D T(string tipo) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Raiz}/texturas_unity/civil_{p}_{tipo}.png");
            mat.SetTexture("_BaseMap", T("BaseColor")); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_WorkflowMode", 0f); mat.EnableKeyword("_SPECULAR_SETUP");          // Specular: RGB = especular, alfa = brilho
            mat.SetTexture("_SpecGlossMap", T("Specular")); mat.SetColor("_SpecColor", Color.white);
            mat.SetFloat("_SmoothnessTextureChannel", 0f); mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_SPECGLOSSMAP");
            mat.SetTexture("_BumpMap", T("Normal")); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            if (ComAlfa.Contains(p))                                                          // cabelo / barba: recorte pelo alfa, dos dois lados
            {
                mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.4f); mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f); mat.renderQueue = 2450;
            }
            EditorUtility.SetDirty(mat);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Civil_" + p), mat);
        }
        AssetDatabase.SaveAssets(); mi.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().First();
        var dedos = mi.humanDescription.human.Count(h => new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Any(f => h.humanName.Contains(f)));
        Debug.Log($"[Civil] avatar valido={avatar.isValid} humanoid={avatar.isHuman} ossos mapeados={mi.humanDescription.human.Length} (dedos: {dedos})");
        return avatar;
    }

'''
s = s[:a] + new + s[b:]
a = s.index("            // eventos (tempo"); b = s.index("            mi.clipAnimations")
s = s[:a] + s[b:]
a = s.index("            var depois"); b = s.index("            foreach (var kv in depois)")
s = s[:a] + '''            var depois = new System.Collections.Generic.Dictionary<string, string> {
                { "Levar_Golpe", "Parado" }, { "Levar_Golpe_Cabeca", "Parado" }, { "Levar_Golpe_Forte", "Parado" },
                { "Concordar", "Parado" }, { "Negar", "Parado" }, { "Interagir", "Parado" } };
''' + s[b:]
s = s.replace('go.name = "Civil3";', 'go.name = "Civil3";').replace("go.AddComponent<ImpactoGolem>().AcharOssos();", "go.AddComponent<CivilNPC>();")
s = s.replace("new Vector3(3f, 2.6f, 7f); Camera.main.transform.LookAt(new Vector3(0, 1.3f, 0));",
              "new Vector3(1.5f, 1.3f, 3.6f); Camera.main.transform.LookAt(new Vector3(0, 1f, 0));")
import os
os.makedirs(r"C:/Users/Thiago/Documentos/rig-civil3/saida/unity/Editor", exist_ok=True)
open(r"C:/Users/Thiago/Documentos/rig-civil3/saida/unity/Editor/MontarCivil.cs", "w", encoding="utf8").write(s)
