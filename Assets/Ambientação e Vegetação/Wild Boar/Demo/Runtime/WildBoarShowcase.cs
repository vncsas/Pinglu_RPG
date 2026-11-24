using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WildBoarDemo
{
    /// <summary>Self-contained recording showcase. Only the selected animal animates.</summary>
    public sealed class WildBoarShowcase : MonoBehaviour
    {
        [Serializable] public sealed class Entry
        {
            public string displayName;
            public GameObject model;
            public Animator animator;
            public AnimationClip[] clips;
            public Vector3 jawLocalAxis;
            public float jawMaximumDegrees;
        }
        public Entry[] animals = Array.Empty<Entry>();
        public Camera showcaseCamera;
        public Transform cameraTarget;
        public string pipelineLabel;
        public int selectedAnimal = 1;
        public bool showInterface = true;
        public bool orbit;
        [Range(0.1f,2)] public float animationSpeed = 1;
        public int SelectedAnimation => clipIndex;
        public bool Paused => paused;
        int clipIndex;
        bool paused;
        double clock;
        PlayableGraph graph;
        AnimationClipPlayable clipPlayable;
        Vector3 cameraHome;
        Quaternion cameraRotation;
        GUIStyle title, sub, small;
        Transform previewJaw;
        Quaternion jawRest;
        float jawOpening;
        int previewLod = -1;
        public float JawOpening => jawOpening;
        public int PreviewLOD => previewLod;

        Vector3 lastMousePosition;
        string notificationText;
        float notificationTimer;
        bool isCapturing;

        void OnEnable()
        {
            if (!showcaseCamera) showcaseCamera = Camera.main ?? FindAnyObjectByType<Camera>();
            if (showcaseCamera) { cameraHome=showcaseCamera.transform.position; cameraRotation=showcaseCamera.transform.rotation; }
            SelectAnimal(selectedAnimal);
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        public void StopPreview() { Release(); }
        void Release() { if(graph.IsValid()) graph.Destroy(); }
        void ResetJaw() { if(previewJaw) previewJaw.localRotation=jawRest; previewJaw=null; jawOpening=0; }
        public void SetJawOpening(float amount)
        {
            jawOpening=Mathf.Clamp01(amount); ApplyJaw();
        }
        void ApplyJaw()
        {
            if(!previewJaw || animals.Length==0)return;
            var entry=animals[selectedAnimal];
            previewJaw.localRotation=jawRest*Quaternion.AngleAxis(entry.jawMaximumDegrees*jawOpening,entry.jawLocalAxis.normalized);
        }
        public void SetPreviewLOD(int level)
        {
            previewLod=Mathf.Clamp(level,-1,2);
            if(animals.Length==0 || !animals[selectedAnimal].model)return;
            foreach(var lod in animals[selectedAnimal].model.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(previewLod);
        }
        public void CycleLOD() { SetPreviewLOD(previewLod==2?-1:previewLod+1); }
        public void SelectAnimal(int index)
        {
            ResetJaw();
            Release();
            if(animals.Length==0) return;
            selectedAnimal=(index%animals.Length+animals.Length)%animals.Length;
            for(int i=0;i<animals.Length;i++) if(animals[i].model) animals[i].model.SetActive(i==selectedAnimal);
            var clips=animals[selectedAnimal].clips??Array.Empty<AnimationClip>();
            int idle=Array.FindIndex(clips,c=>c && c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);
            SelectAnimation(idle>=0?idle:0);FrameSelectedAnimal(selectedAnimal);SetPreviewLOD(-1);
            var entry=animals[selectedAnimal];
            if(entry.jawMaximumDegrees>0 && entry.jawLocalAxis.sqrMagnitude>.5f) {
                previewJaw=Array.Find(entry.model.GetComponentsInChildren<Transform>(true),t=>t.name=="Jaw");
                if(previewJaw)jawRest=previewJaw.localRotation;
            }
        }
        public void FrameSelectedAnimal(int index)
        {
            if(!showcaseCamera||!cameraTarget||index<0||index>=animals.Length||!animals[index].model)return;
            var model=animals[index].model;
            var skin=Array.Find(model.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.name=="Body");
            Vector3 center;float length;
            if(skin&&skin.sharedMesh) {
                var bounds=skin.sharedMesh.bounds;center=skin.transform.TransformPoint(bounds.center);
                length=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*skin.transform.lossyScale.x;
            } else {
                var renderers=model.GetComponentsInChildren<MeshRenderer>(true);if(renderers.Length==0)return;
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                center=bounds.center;length=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            }
            cameraTarget.position=center;
            showcaseCamera.transform.position=center+new Vector3(2.8f,.95f,2.4f).normalized*Mathf.Max(length,.01f)*2.1f;
            showcaseCamera.transform.LookAt(center);cameraHome=showcaseCamera.transform.position;cameraRotation=showcaseCamera.transform.rotation;
        }
        public void SelectAnimation(int index)
        {
            if(previewJaw)previewJaw.localRotation=jawRest; jawOpening=0;
            Release();
            if(animals.Length==0) return;
            var entry=animals[selectedAnimal];
            if(!entry.animator || entry.clips==null || entry.clips.Length==0) return;
            clipIndex=(index%entry.clips.Length+entry.clips.Length)%entry.clips.Length;
            var clip=entry.clips[clipIndex]; if(!clip) return;
            // Clear the previous clip's eyelid state before evaluating the next clip.
            foreach(var skin in entry.model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if(!skin.sharedMesh)continue;
                int closed=skin.sharedMesh.GetBlendShapeIndex("EyesClosed");
                if(closed>=0)skin.SetBlendShapeWeight(closed,0f);
            }
            entry.animator.applyRootMotion=false;
            entry.animator.runtimeAnimatorController=null;
            entry.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Ultrarealistic Forest Animals showcase");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable=AnimationClipPlayable.Create(graph,clip);
            clipPlayable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Animal",entry.animator).SetSourcePlayable(clipPlayable);
            clock=0; graph.Play(); graph.Evaluate(0);
        }
        public void TogglePause() { paused=!paused; }
        public void SetPreviewTime(double time)
        {
            if (!graph.IsValid() || animals.Length == 0) return;
            clock = Math.Max(0, Math.Min(time, animals[selectedAnimal].clips[clipIndex].length));
            clipPlayable.SetTime(clock); graph.Evaluate(0);
            ApplyJaw();
        }

        void Update()
        {
            HandleInput();

            if(graph.IsValid() && !paused) {
                var clip=animals[selectedAnimal].clips[clipIndex]; var length=clip.length;
                double next = clock + Time.unscaledDeltaTime*animationSpeed;
                clock=clip.isLooping ? next % Math.Max(.001,length) : Math.Min(next, length);
                clipPlayable.SetTime(clock); graph.Evaluate(0);
            }
            if(orbit && showcaseCamera) {
                Vector3 target = GetTargetPosition();
                showcaseCamera.transform.RotateAround(target,Vector3.up,Time.unscaledDeltaTime*8);
                showcaseCamera.transform.LookAt(target);
            }
            ApplyJaw();
        }

        void HandleInput()
        {
            float scroll = 0f;
            Vector2 mouseDelta = Vector2.zero;
            bool isRotating = false;

#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.rightArrowKey.wasPressedThisFrame) SelectAnimal(selectedAnimal + 1);
                if (k.leftArrowKey.wasPressedThisFrame) SelectAnimal(selectedAnimal - 1);
                if (k.upArrowKey.wasPressedThisFrame) SelectAnimation(clipIndex + 1);
                if (k.downArrowKey.wasPressedThisFrame) SelectAnimation(clipIndex - 1);
                if (k.spaceKey.wasPressedThisFrame) TogglePause();
                if (k.enterKey.wasPressedThisFrame) { SelectAnimation(clipIndex); paused = false; }
                if (k.hKey.wasPressedThisFrame) showInterface = !showInterface;
                if (k.oKey.wasPressedThisFrame) orbit = !orbit;
                if (k.rKey.wasPressedThisFrame) ResetCamera();
                if (k.jKey.wasPressedThisFrame && previewJaw) SetJawOpening(jawOpening>.5f?0:1);
                if (k.lKey.wasPressedThisFrame) CycleLOD();
                if (k.digit1Key.wasPressedThisFrame) SelectAnimal(0);
                if (k.digit2Key.wasPressedThisFrame) SelectAnimal(1);
                if (k.digit3Key.wasPressedThisFrame) SelectAnimal(2);
                if (k.digit4Key.wasPressedThisFrame) SelectAnimal(3);
                if (k.digit8Key.wasPressedThisFrame) SelectAnimal(7);
                if (k.digit7Key.wasPressedThisFrame) SelectAnimal(6);
                if (k.digit6Key.wasPressedThisFrame) SelectAnimal(5);
                if (k.digit5Key.wasPressedThisFrame) SelectAnimal(4);

                bool ctrl = k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
                bool shift = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                if (ctrl && shift && k.kKey.wasPressedThisFrame) TakeScreenshot();
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scrollVal = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollVal) > 0.01f)
                {
                    scroll = Mathf.Abs(scrollVal) >= 10f ? scrollVal / 120f : scrollVal;
                }

                isRotating = mouse.rightButton.isPressed || mouse.leftButton.isPressed;
                if (isRotating)
                {
                    mouseDelta = mouse.delta.ReadValue();
                    if (mouseDelta.sqrMagnitude > 10000f) mouseDelta = Vector2.zero;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.RightArrow)) SelectAnimal(selectedAnimal + 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectAnimal(selectedAnimal - 1);
            if (Input.GetKeyDown(KeyCode.UpArrow)) SelectAnimation(clipIndex + 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) SelectAnimation(clipIndex - 1);
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Return)) { SelectAnimation(clipIndex); paused = false; }
            if (Input.GetKeyDown(KeyCode.H)) showInterface = !showInterface;
            if (Input.GetKeyDown(KeyCode.O)) orbit = !orbit;
            if (Input.GetKeyDown(KeyCode.R)) ResetCamera();
            if (Input.GetKeyDown(KeyCode.J) && previewJaw) SetJawOpening(jawOpening>.5f?0:1);
            if (Input.GetKeyDown(KeyCode.L)) CycleLOD();
            for (int i = 0; i < Math.Min(8, animals.Length); i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SelectAnimal(i);

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (ctrl && shift && Input.GetKeyDown(KeyCode.K)) TakeScreenshot();

            scroll = Input.mouseScrollDelta.y;
            bool pressed = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(0);
            if (pressed) lastMousePosition = Input.mousePosition;
            isRotating = Input.GetMouseButton(1) || Input.GetMouseButton(0);
            if (isRotating && !pressed)
            {
                mouseDelta = (Vector2)(Input.mousePosition - lastMousePosition);
            }
            lastMousePosition = Input.mousePosition;
#endif

            if (Mathf.Abs(scroll) > 0.001f)
            {
                ZoomCamera(scroll);
            }

            if (isRotating && mouseDelta.sqrMagnitude > 0.01f)
            {
                orbit = false;
                RotateCamera(mouseDelta.x, mouseDelta.y);
            }
        }

        void ZoomCamera(float scroll)
        {
            if (!showcaseCamera || Mathf.Abs(scroll) < 0.0001f) return;
            Vector3 target = GetTargetPosition();
            Vector3 offset = showcaseCamera.transform.position - target;
            float dist = offset.magnitude;
            if (dist < 0.01f) dist = 0.01f;

            float zoomFactor = 1f - Mathf.Clamp(scroll, -3f, 3f) * 0.1f;
            float newDist = Mathf.Clamp(dist * zoomFactor, 0.2f, 40f);
            showcaseCamera.transform.position = target + offset.normalized * newDist;
        }

        void RotateCamera(float deltaX, float deltaY)
        {
            if (!showcaseCamera) return;
            Vector3 target = GetTargetPosition();
            float sensitivity = 0.25f;

            showcaseCamera.transform.RotateAround(target, Vector3.up, deltaX * sensitivity);

            Vector3 right = showcaseCamera.transform.right;
            float pitch = -deltaY * sensitivity;
            Vector3 forward = showcaseCamera.transform.forward;
            Vector3 newForward = Quaternion.AngleAxis(pitch, right) * forward;

            if (Mathf.Abs(Vector3.Dot(newForward, Vector3.up)) < 0.96f)
            {
                showcaseCamera.transform.RotateAround(target, right, pitch);
            }
            showcaseCamera.transform.LookAt(target);
        }

        Vector3 GetTargetPosition()
        {
            if (cameraTarget) return cameraTarget.position;
            if (animals != null && selectedAnimal >= 0 && selectedAnimal < animals.Length && animals[selectedAnimal] != null && animals[selectedAnimal].model)
                return animals[selectedAnimal].model.transform.position + Vector3.up * 0.6f;
            return Vector3.zero;
        }

        public void TakeScreenshot()
        {
            if (isCapturing) return;
            StartCoroutine(CaptureRoutine());
        }

        IEnumerator CaptureRoutine()
        {
            isCapturing = true;
            bool prevInterface = showInterface;
            showInterface = false;

            yield return null;

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string animalName = "Unknown";
            if (animals != null && selectedAnimal >= 0 && selectedAnimal < animals.Length && animals[selectedAnimal] != null)
            {
                animalName = animals[selectedAnimal].displayName.Replace(" ", "_").Replace("&", "and");
            }
            string filename = Path.Combine(folder, $"UltrarealisticForestAnimals_{animalName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            ScreenCapture.CaptureScreenshot(filename);

            yield return new WaitForEndOfFrame();
            yield return null;

            showInterface = prevInterface;
            notificationText = $"SCREENSHOT SAVED: {Path.GetFileName(filename)}";
            notificationTimer = 3.5f;
            isCapturing = false;
            Debug.Log($"[WildBoarShowcase] Screenshot saved: {filename}");
        }

        public void ResetCamera() { orbit=false;if(showcaseCamera) showcaseCamera.transform.SetPositionAndRotation(cameraHome,cameraRotation); }

        void OnGUI()
        {
            if(title==null) {
                title=new GUIStyle(GUI.skin.label){fontSize=38,fontStyle=FontStyle.Bold};
                sub=new GUIStyle(GUI.skin.label){fontSize=19};
                small=new GUIStyle(GUI.skin.label){fontSize=13};
                title.normal.textColor=new Color(.94f,.91f,.83f);
                sub.normal.textColor=new Color(.82f,.69f,.46f);
                small.normal.textColor=new Color(.77f,.79f,.80f);
            }
            var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*(Screen.width/1920f));
            float h=Screen.height*1920f/Screen.width;

            if(notificationTimer > 0 && !isCapturing) {
                notificationTimer -= Time.unscaledDeltaTime;
                var notifStyle = new GUIStyle(GUI.skin.box){fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
                notifStyle.normal.textColor = new Color(0.35f, 0.95f, 0.55f);
                GUI.Box(new Rect(1920f/2f - 350, 24, 700, 40), notificationText, notifStyle);
            }

            if(!showInterface || animals.Length==0) {
                GUI.matrix=old;
                return;
            }

            GUI.Label(new Rect(64,44,1500,32),"W I L D   B O A R   /   F R E E   D E M O",sub);
            GUI.Label(new Rect(64,82,1000,64),animals[selectedAnimal].displayName,title);
            var clips=animals[selectedAnimal].clips??Array.Empty<AnimationClip>();
            GUI.Label(new Rect(66,146,900,30),pipelineLabel+"   /   "+(clips.Length>0?clips[Mathf.Clamp(clipIndex,0,clips.Length-1)].name:"Static model")+ (paused?"   /   PAUSED":""),small);
            GUI.Label(new Rect(66,176,900,30),"LOD "+(previewLod<0?"AUTO":previewLod.ToString())+"   /   L  CHANGE LOD"+(previewJaw?"   /   J  OPEN/CLOSE JAW":""),small);
            if(previewJaw) { GUI.Label(new Rect(66,210,120,26),"Jaw opening",small); float amount=GUI.HorizontalSlider(new Rect(190,219,200,18),jawOpening,0,1); if(Mathf.Abs(amount-jawOpening)>.001f)SetJawOpening(amount); }
            GUI.Label(new Rect(64,h-65,1800,30),"↑ ↓  ANIMATION     SPACE  PAUSE     ENTER  REPLAY     O  ORBIT     LMB/RMB  ROTATE     SCROLL  ZOOM     CTRL+SHIFT+K  SCREENSHOT     R  RESET     H  CLEAN VIEW",small);
            GUI.Label(new Rect(64,246,1100,28),"From Ultrarealistic Forest Animals — explore the complete eight-animal collection",small);
            if(GUI.Button(new Rect(64,282,360,36),"Find Ultrarealistic Forest Animals"))
                Application.OpenURL(WildBoarLinks.FullCollectionUrl);
            GUI.Label(new Rect(1510,80,330,30),"ANIMATIONS",sub);
            for(int i=0;i<clips.Length;i++) {
                string label=clips[i].name.Replace("Attack_Bite_Forward_Lunge_Tear","Bite / Lunge / Tear").Replace("Attack_Charge_Gore","Charge / Gore").Replace("_"," ");
                if(GUI.Button(new Rect(1510,122+i*42,330,34),(i==clipIndex?"• ":"")+label)) {
                    SelectAnimation(i); paused=false;
                }
            }
            if(GUI.Button(new Rect(1510,122+clips.Length*42,160,34),paused?"Resume":"Pause"))TogglePause();
            if(GUI.Button(new Rect(1680,122+clips.Length*42,160,34),"Replay")) { SelectAnimation(clipIndex); paused=false; }
            GUI.matrix=old;
        }
    }
}
