B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"; D=C:/Users/Thiago/Documentos/rig-golem
U1="C:/Users/Thiago/Documentos/Universal Animation Library [Standard]/Animation Library[Standard]/Unity/AnimationLibrary_Unity_Standard.fbx"
U2="C:/Users/Thiago/Documentos/Universal Animation Library 2[Standard]/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx"
"$B" -b $D/work/norm.blend -P $D/scripts/build.py -- $D/scripts $D/work/rig_a.blend 150000 2>&1 | grep -E "pad|Error|Trace|line [0-9]"
"$B" -b $D/work/rig_a.blend -P $D/scripts/rig_b.py -- $D/saida/texturas_unity $D/saida 2>&1 | grep -E "Error|Trace|line [0-9]"
"$B" -b $D/saida/golem_rig.blend -P $D/scripts/retarget.py -- "$U1" $D/work/rt1.blend Idle_Loop=Parado Walk_Loop=Andar Jog_Fwd_Loop=Correr Hit_Chest=Levar_Golpe Death01=Morrer Punch_Cross=Soco_Direto 2>&1 | grep -E "Error|Trace"
"$B" -b $D/work/rt1.blend -P $D/scripts/retarget.py -- "$U2" $D/work/anim_rt.blend Melee_Hook=Varrer Hit_Knockback=Levar_Golpe_Forte 2>&1 | grep -E "Error|Trace"
"$B" -b $D/work/anim_rt.blend -P $D/scripts/golem_anim.py -- $D/scripts $D/work/anim_final.blend 2>&1 | grep -E "ACTION|DONE|Error|Trace|line [0-9]|File"
