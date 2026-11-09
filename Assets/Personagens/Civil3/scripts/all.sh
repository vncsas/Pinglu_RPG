B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"; D=C:/Users/Thiago/Documentos/rig-civil3
"$B" -b -P $D/scripts/prep.py -- "$D/src/Civilian 3.fbx" $D/work/rig_a.blend 2>&1 | grep -E "max infl|Error|Trace|line [0-9]"
"$B" -b $D/work/rig_a.blend -P $D/scripts/rig_b.py -- $D/saida/texturas_unity $D/saida 2>&1 | grep -E "^\[|Error|Trace|line [0-9]"
U1="C:/Users/Thiago/Documentos/Universal Animation Library [Standard]/Animation Library[Standard]/Unity/AnimationLibrary_Unity_Standard.fbx"
U2="C:/Users/Thiago/Documentos/Universal Animation Library 2[Standard]/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx"
"$B" -b $D/saida/civil_rig.blend -P $D/scripts/retarget.py -- "$U1" $D/work/rt1.blend Idle_Loop=Parado Walk_Loop=Andar Walk_Formal_Loop=Andar_Calmo Jog_Fwd_Loop=Correr Sprint_Loop=Correr_Base Idle_Talking_Loop=Falar Hit_Chest=Levar_Golpe Hit_Head=Levar_Golpe_Cabeca Death01=Morrer Interact=Interagir 2>&1 | grep -E "RETARGET|Error|Trace"
"$B" -b $D/work/rt1.blend -P $D/scripts/retarget.py -- "$U2" $D/work/anim_rt.blend Hit_Knockback=Levar_Golpe_Forte Yes=Concordar Idle_No_Loop=Negar Idle_FoldArms_Loop=Ouvir 2>&1 | grep -E "RETARGET|Error|Trace"
"$B" -b $D/work/anim_rt.blend -P $D/scripts/civil_anim.py -- $D/scripts $D/work/anim_final.blend 2>&1 | grep -E "ACTION|DONE|Error|Trace|line [0-9]|File"
