WILD BOAR 1.0.0
Free standalone sample of Ultrarealistic Forest Animals
Rip Vertices Studio

QUICK START
1. Import the complete Wild_Boar_AllRP_1.0.0.unitypackage.
2. Choose Tools > Wild Boar > Open demo for current pipeline.
3. Enter Play and use Up/Down to choose an animation.

The package is under Assets/Wild Boar. The full collection is not required.
Built-in, URP and HDRP assets are included together. Use the scene and
prefabs matching your project's render pipeline.

Prepared with Unity 2022.3.62f3. URP/HDRP assets use the corresponding
official Unity render-pipeline packages 14.0.12. Install the matching
package through Window > Package Manager when using an SRP project.
Built-in does not require URP or HDRP. The showcase uses Unity's Animation,
IMGUI, Image Conversion and Screen Capture modules; keep these enabled.

Open demo for current pipeline opens a scene without changing your renderer.
To use the supplied studio settings in a test project, choose:
  Tools > Wild Boar > Setup showcase > Built-in
  Tools > Wild Boar > Setup showcase > URP
  Tools > Wild Boar > Setup showcase > HDRP
Setup applies the selected demo pipeline asset, clears the active Quality
pipeline override and opens its scene. Existing projects can keep their
own renderer and use Open demo for current pipeline instead.

Scenes are in Assets/Wild Boar/Demo/Scenes:
  WildBoar_Demo_Built-in.unity
  WildBoar_Demo_URP.unity
  WildBoar_Demo_HDRP.unity

PREFABS AND CONTENT
Drag Prefabs/<pipeline>/HD/Wild_Boar into your scene for fur and LODs.
Use Prefabs/<pipeline>/Mobile/Wild_Boar for the body-only variant.
The root carries the Animator/controller and, for HD, an LODGroup.
Keep the supplied skeleton and skinned-renderer references together.
Prefab roots have position/rotation zero and scale (1,1,1).

Animals/Wild_Boar contains Source/Wild_Boar.fbx, the Generic rig, mesh
assets, materials, textures, eight .anim clips and Wild_Boar.controller.
Prefabs/SourceModels/Wild_Boar_SourceBody is a body-only inspection prefab
referencing the FBX mesh directly, with Built-in materials.

ANIMATION
Clip/state name                         Duration   Loop
Idle                                   8.000 s    Yes
Walking                                1.067 s    Yes
Run                                    0.600 s    Yes
Jump                                   1.333 s    No
Get_Hit                                1.000 s    No
Dying                                  2.600 s    No
Attack_Bite_Forward_Lunge_Tear           1.500 s    No
Attack_Charge_Gore                      1.500 s    No

Use the prefab's root Animator in your own scene:
  Animator animator = boar.GetComponent<Animator>();
  animator.Play("Idle", 0, 0f);
  animator.CrossFadeInFixedTime("Run", 0.15f, 0);
  animator.SetTrigger("GetHit");

Use the exact state names above with Play or CrossFadeInFixedTime.
GetHit plays Get_Hit and returns to Idle through the supplied controller.
Choose the next state explicitly after Jump, an attack or Dying. Locomotion
is in place: move the prefab root with your own character/gameplay logic.
The showcase previews clips through a PlayableGraph; use a separate prefab
instance when testing your controller logic. No animal AI or interactive
physics ragdoll is included.

SHOWCASE CONTROLS
Up/Down: animation. Space: pause. Enter: replay. L: automatic/LOD0/LOD1/LOD2.
J or jaw slider: jaw opening. O: automatic camera orbit. R: reset camera.
LMB/RMB drag: rotate camera. Mouse wheel: zoom. H: hide/show interface.
Ctrl+Shift+K: save a screenshot in the project's Screenshots directory.
The demo contains only the boar. Its full-collection button opens an
Asset Store search for Ultrarealistic Forest Animals when clicked.

FUR MATERIALS — OFFLINE GUIDE
Select a shell or card material under Animals/Wild_Boar/Materials/<pipeline>.
The custom Inspector provides Tint, Length, Strand density, Tip taper,
Root shade and Tip shade, with Lighting, Grooming and Texture maps groups.
Duplicate materials before creating a new look; apply changes to both
shell LOD materials and the card material when appropriate.

Length scales the coat; MaxLength sets its maximum displacement.
Strand density, Tip taper, LengthJitter and Clumping alter the strand pattern.
CombStrength, CombSurfaceAdherence, CombUprightness and CombJitter modify
how the control-map grooming is applied. Gravity/GravityStrength alter bend.
Lighting contains RootAO, Smoothness, AnisoSpecular, Rim, DiffuseWrap,
ShadowOpacity, AlbedoInfluence and ColorVar. Texture maps allows normal
and smoothness inputs to be enabled and the smoothness channel selected.
For the supplied Specular_roughness input, RoughnessInput is enabled and
SmoothnessChannel is R. Keep bare facial areas excluded by the coat mask.
These controls edit materials; this sample does not include a fur-paint tool.

TEXTURE MAPS AND CHANNELS
Texture                                Size       Channels / interpretation
Base_color                             4096       RGB albedo, sRGB
Normal_OpenGL                          4096       RGB tangent-space normal;
                                                  imported as Normal Map
Specular_roughness                      4096       R roughness input, linear;
                                                  fur reads smoothness=1-R
BodySpecularGloss                       2048       RGB specular, A smoothness
MetallicOcclusionSmoothness             2048       R metallic, G occlusion,
                                                  B detail mask, A smoothness
FurControl                             1024       R length, G coat coverage,
                                                  BA tangent-space grooming
Mobile_Albedo                          1024       RGB albedo, sRGB
Mobile_BodySpecularGloss                1024       RGB specular, A smoothness
Mobile_MetallicOcclusionSmoothness      1024       R metallic, G occlusion,
                                                  B detail mask, A smoothness

All dimensions are square pixels. Packed runtime and FurControl maps are
linear. FurControl BA decodes as 2*channel-1; neutral direction is (.5,.5).
R/G zero excludes fur. Built-in/URP body materials use the specular workflow;
HDRP uses its corresponding specular-color/mask configuration. Keep the
supplied material assignments when switching pipelines.

QUALITY AND GEOMETRY
Body / HD LOD2 / Mobile: 6,963 vertices, 12,454 triangles.
Complete HD LOD0: 136,323 vertices, 234,460 triangles (24 shell layers/cards).
Complete HD LOD1: 70,095 vertices, 122,296 triangles (12 shell layers/cards).
HD LOD2 draws the body alone. The default screen-relative thresholds are
0.45, 0.16 and 0.012; below the final threshold the animal culls.
Use the prefab LODGroup to tune these distances for your camera.
Material density does not reduce the baked mesh's triangle count.

Mobile keeps the same body mesh and rig, removes shell/card fur, and uses
1024 maps without a normal map. It has no LODGroup and is not a decimated
mesh. Choose quality for your target hardware and number of visible animals.
TAA can improve fine fur stability where supported by your render pipeline.

FULL COLLECTION
Wild Boar includes one complete animal without a timer or unlock requirement.
The current Ultrarealistic Forest Animals collection also contains Bear,
Fox, Wolf, Hart (deer stag), Doe, Moose (bull) and Female Moose (cow).
Tools > Wild Boar > View full animal collection opens the configured
Asset Store destination. The default is an exact-title search.
The URL is in Demo/Runtime/WildBoarLinks.cs, FullCollectionUrl.

This standalone folder, GUIDs, shader names and WildBoarDemo namespace
can coexist with the full collection. Importing the full product does not
update this sample in place. Package documentation is accessible through
Tools > Wild Boar > Read documentation.

Publisher: https://assetstore.unity.com/publishers/44941

