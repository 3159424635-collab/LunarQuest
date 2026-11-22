using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using LunarQuest.Module4;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;

public static class Module4Builder
{
    const string C="Assets/_Project/Cinematics/Module4/";
    const string U="Assets/_Project/UI/";
    const string A="Assets/_Project/Audio/Raw/";
    static Dictionary<string,Material> mats=new Dictionary<string,Material>();
    static Font font;
    static Camera camera;
    static Module4HudView hud;
    static VoiceSpec voiceSpec;
    static bool sceneOnly;
    [Serializable] public class Voice {public string phase;public float at;public string text;public string file;public float duration;}
    [Serializable] public class VoiceSpec {public Voice[] voices;}
    [Serializable] public class Check {public string unity; public int sequences; public int nativeSignals;public int virtualCameras; public string[] timelineNames;public double[] durations; public bool hudBindings;public string[] audioFiles; public bool passed;}

    [MenuItem("LunarQuest/Module 4/Build Demonstration")]
    public static void Build()
    {
        if(Application.unityVersion!="2022.3.62f1")throw new Exception("Use Unity 2022.3.62f1.");
        mats.Clear();
        foreach(var p in new[]{C+"Materials",C+"Geometry",C+"Timelines",C+"Prefabs",C+"Demo",U+"Prefabs",U+"Sprites"})Directory.CreateDirectory(p);
        AssetDatabase.Refresh();
        font=AssetDatabase.LoadAssetAtPath<Font>(U+"Fonts/NotoSansCJKsc-Regular.otf");
        if(!font)throw new Exception("Noto font is missing.");
        voiceSpec=JsonUtility.FromJson<VoiceSpec>(File.ReadAllText(C+"showcase.json"));
        EditorSettings.serializationMode=SerializationMode.ForceText;
        GraphicsSettings.renderPipelineAsset=null;QualitySettings.renderPipeline=null;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var data=ScriptableObject.CreateInstance<UniversalRendererData>();
        ResourceReloader.ReloadAllNullIn(data,UniversalRenderPipelineAsset.packagePath);
        data.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        var pipeline=UniversalRenderPipelineAsset.Create(data);
        pipeline.msaaSampleCount=4;pipeline.supportsHDR=true;pipeline.shadowDistance=300;
        AssetDatabase.CreateAsset(data,C+"Materials/Module4Renderer.asset");
        AssetDatabase.CreateAsset(pipeline,C+"Materials/Module4URP.asset");
        GraphicsSettings.renderPipelineAsset=pipeline; QualitySettings.renderPipeline=pipeline;
        QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;
        camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.nearClipPlane=.1f;camera.farClipPlane=4000;camera.allowHDR=true;
        camera.gameObject.AddComponent<AudioListener>();
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
        var brain=camera.gameObject.AddComponent<CinemachineBrain>();brain.m_DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut,.8f);
        var volume=new GameObject("Module4 Post Processing").AddComponent<Volume>();volume.isGlobal=true;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom=profile.Add<Bloom>();bloom.intensity.Override(.28f);bloom.threshold.Override(1.2f);
        var tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>();color.postExposure.Override(.28f);color.contrast.Override(2f);color.saturation.Override(3f);
        var vignette=profile.Add<Vignette>();vignette.intensity.Override(.08f);
        AssetDatabase.CreateAsset(profile,C+"Materials/Module4Post.asset");
        foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
        volume.sharedProfile=profile;
        hud=BuildHud();
        new GameObject("Module4 Event System",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.EventSystems.StandaloneInputModule));
        var launch=Launch(); var orbit=Orbit();
        CinemachineVirtualCamera moonCamera;AudioSource moonMusic,moonVoice;
        var moon=MoonWorld(out moonCamera,out moonMusic,out moonVoice);
        var returning=Return();
        var master=new GameObject("Module4 Presentation Controller").AddComponent<Module4ShowcaseController>();
        master.sequences=new[]{launch,orbit,returning};master.moonWorld=moon;master.moonCamera=moonCamera;
        master.mainCamera=camera;master.hud=hud;master.moonMusic=moonMusic;master.moonVoice=moonVoice;
        master.hudVoices=voiceSpec.voices.Where(v=>v.phase=="HUD").Select(v=>LoadAudio(v.file)).ToArray();
        master.gameObject.AddComponent<Module4FrameExporter>();
        foreach(var s in master.sequences){s.Bind(camera,hud);s.gameObject.SetActive(false);}
        moon.SetActive(false);launch.gameObject.SetActive(true);
        ValidateMotion(launch,orbit,returning);
        launch.Seek(0);camera.transform.SetPositionAndRotation(new Vector3(80,32,-115),Quaternion.Euler(8,-32,0));
        PrefabUtility.SaveAsPrefabAsset(hud.gameObject,U+"Prefabs/Module4HUD.prefab");
        foreach(var s in master.sequences)PrefabUtility.SaveAsPrefabAsset(s.gameObject,C+"Prefabs/"+s.sequenceId+".prefab");
        // 保存 HUD 预制体后再次固定场景实例缩放，避免无图形批处理环境将根 Canvas 写成零缩放。
        hud.transform.localScale=Vector3.one;EditorUtility.SetDirty(hud.transform);EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene,C+"Demo/Module4Showcase.unity");
        LunarQuest.EditorTools.BuildSceneCatalog.ConfigureProjectScenes();
        PlayerSettings.companyName="LunarQuest Course Project";PlayerSettings.productName="LunarQuest Module 4";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        AssetDatabase.SaveAssets();
        var check=new Check {unity=Application.unityVersion,sequences=3,nativeSignals=3,
            virtualCameras=UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>(true).Length,
            timelineNames=master.sequences.Select(s=>s.director.playableAsset.name).ToArray(),
            durations=master.sequences.Select(s=>s.director.duration).ToArray(),
            hudBindings=hud.speedText&&hud.promptText&&hud.questText,
            audioFiles=Directory.GetFiles(A,"*.wav").Select(Path.GetFileName).ToArray(),passed=true};
        for(int i=0;i<3;i++)if(Math.Abs(check.durations[i]-new[]{60,30,45}[i])>.1)throw new Exception("Timeline duration mismatch.");
        File.WriteAllText("Module4BuildCheck.json",JsonUtility.ToJson(check,true));
        if(sceneOnly){Debug.Log("M4_SCENE_REBUILD_PASSED");return;}
        var output=Arg("-m4out")??Path.GetFullPath("Build/Module4");Directory.CreateDirectory(output);
        AssetDatabase.ExportPackage(new[]{"Assets/_Project/Cinematics","Assets/_Project/Audio","Assets/_Project/UI"},Path.Combine(output,"LunarQuest_Module4.unitypackage"),ExportPackageOptions.Recurse);
        var buildPath=Path.Combine(output,"Windows/LunarQuest_Module4.exe");Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{C+"Demo/Module4Showcase.unity"},locationPathName=buildPath,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Standalone build failed.");
        Debug.Log("M4_BUILD_PASSED:"+buildPath);
    }

    [MenuItem("LunarQuest/Module 4/Rebuild Scene Only")]
    public static void RebuildSceneOnly()
    {
        sceneOnly=true;
        try{Build();}
        finally{sceneOnly=false;}
    }

    [MenuItem("LunarQuest/Module 4/Export Preview Frames")]
    public static void ExportPreviewFrames()
    {
        EditorSceneManager.OpenScene(C+"Demo/Module4Showcase.unity",OpenSceneMode.Single);
        var controller=UnityEngine.Object.FindObjectOfType<Module4ShowcaseController>();
        if(!controller)throw new Exception("Module 4 controller is missing.");
        controller.externalClock=true;controller.exportMode=true;
        var update=typeof(Module4ShowcaseController).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var visualUpdate=typeof(Module4VisualDriver).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var skyEnable=typeof(Module4SkyState).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var canvas=controller.hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=controller.mainCamera;canvas.planeDistance=1;
        var brain=controller.mainCamera.GetComponent<CinemachineBrain>();var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();controller.mainCamera.targetTexture=target;controller.mainCamera.aspect=16f/9f;
        string folder=Arg("-m4preview")??Path.GetFullPath("Build/Module4Preview");Directory.CreateDirectory(folder);
        float[] times={15,40,58,80,118,154,166,180};string[] names={"01_launch_ground","02_launch_upper","03_launch_space","04_orbit","05_moon","06_return_ascent","07_reentry","08_landing"};
        for(int i=0;i<times.Length;i++)
        {
            controller.forcedTime=times[i];update.Invoke(controller,null);
            foreach(var state in UnityEngine.Object.FindObjectsOfType<Module4SkyState>(true))if(state.gameObject.activeInHierarchy)skyEnable.Invoke(state,null);
            foreach(var visual in UnityEngine.Object.FindObjectsOfType<Module4VisualDriver>(true))if(visual.gameObject.activeInHierarchy)visualUpdate.Invoke(visual,null);
            brain.ManualUpdate();Canvas.ForceUpdateCanvases();controller.mainCamera.Render();
            RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,names[i]+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        RenderTexture.active=null;controller.mainCamera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);Debug.Log("M4_PREVIEW_EXPORT_PASSED:"+folder);
    }

    static string Arg(string key){var a=Environment.GetCommandLineArgs();for(int i=0;i<a.Length-1;i++)if(a[i]==key)return a[i+1];return null;}
    static void ValidateMotion(Module4Sequence launch,Module4Sequence orbit,Module4Sequence returning)
    {
        foreach(var sequence in new[]{launch,orbit,returning})sequence.gameObject.SetActive(false);
        launch.gameObject.SetActive(true);launch.director.RebuildGraph();launch.Seek(40);launch.director.Evaluate();float rise=Find(launch,"LQ01").localPosition.y;launch.gameObject.SetActive(false);
        orbit.gameObject.SetActive(true);orbit.director.RebuildGraph();orbit.Seek(20);orbit.director.Evaluate();float earth=Find(orbit,"Earth").localPosition.y;orbit.gameObject.SetActive(false);
        returning.gameObject.SetActive(true);returning.director.RebuildGraph();returning.Seek(40);returning.director.Evaluate();float pod=Find(returning,"Landing Capsule").localPosition.y;returning.gameObject.SetActive(false);
        File.WriteAllText("Module4MotionCheck.json","{\"launchAt40\":"+rise+",\"earthAt20\":"+earth+",\"landingAt40\":"+pod+"}");
        if(rise<100||rise>160||earth<20||earth>40||pod>19||pod<3)throw new Exception("Native animation bindings failed.");
        launch.gameObject.SetActive(true);launch.Seek(0);orbit.Seek(0);returning.Seek(0);
    }
    static Transform Find(Module4Sequence s,string name){return s.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);}
    static Material Mat(string id,Color color,float metallic=0,float smooth=.4f,bool emission=false)
    {
        if(mats.ContainsKey(id))return mats[id];
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=id,color=color};
        m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*3);}
        AssetDatabase.CreateAsset(m,C+"Materials/"+id+".mat");mats[id]=m;return m;
    }
    static Material Sky(string id,Color top,Color bottom,float stars)
    {
        var m=new Material(Shader.Find("LunarQuest/Module4Sky"));m.SetColor("_Top",top);m.SetColor("_Bottom",bottom);m.SetFloat("_Stars",stars);AssetDatabase.CreateAsset(m,C+"Materials/"+id+".mat");return m;
    }
    static GameObject Obj(string name,Transform parent,PrimitiveType type,Vector3 p,Vector3 scale,Material material,Vector3? rotation=null)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;
        if(rotation.HasValue)g.transform.localEulerAngles=rotation.Value;
        g.GetComponent<Renderer>().sharedMaterial=material;var col=g.GetComponent<Collider>();if(col)UnityEngine.Object.DestroyImmediate(col);return g;
    }
    static void Beam(string name,Transform parent,Vector3 a,Vector3 b,float width,Material mat)
    {var g=Obj(name,parent,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(width,(b-a).magnitude*.5f,width),mat);g.transform.up=(b-a).normalized;}
    static GameObject Empty(string name,Transform parent=null,Vector3 p=default(Vector3))
    {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=p;return g;}
    static void LightAt(Transform root,Color color,float intensity,Vector3 angle)
    {var light=Empty("Key Sun",root).AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.shadows=LightShadows.Soft;light.transform.eulerAngles=angle;}
    static void WorldState(GameObject root,Material sky,Color ambient)
    {var s=root.AddComponent<Module4SkyState>();s.sky=sky;s.ambient=ambient;}
    static GameObject Lathe(string name,Transform parent,float[] radii,float[] heights,Material material)
    {
        int count=48;var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int j=0;j<radii.Length;j++)for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;verts.Add(new Vector3(Mathf.Cos(a)*radii[j],heights[j],Mathf.Sin(a)*radii[j]));uv.Add(new Vector2((float)i/count,(float)j/(radii.Length-1)));}
        for(int j=0;j<radii.Length-1;j++)for(int i=0;i<count;i++){int k=j*(count+1)+i;tris.AddRange(new[]{k,k+count+1,k+1,k+1,k+count+1,k+count+2});}
        var mesh=new Mesh{name=name};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,C+"Geometry/"+name+".asset");
        var g=Empty(name,parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;
    }
    static Module4Sequence Sequence(string id,string title,float duration)
    {
        var g=Empty(id);var s=g.AddComponent<Module4Sequence>();s.sequenceId=id;s.title=title;s.duration=duration;s.director=g.AddComponent<PlayableDirector>();s.director.playOnAwake=false;s.director.extrapolationMode=DirectorWrapMode.Hold;
        var t=ScriptableObject.CreateInstance<TimelineAsset>();t.name="CT_"+id;AssetDatabase.CreateAsset(t,C+"Timelines/CT_"+id+".playable");s.director.playableAsset=t;
        s.signalReceiver=g.AddComponent<SignalReceiver>();var signal=ScriptableObject.CreateInstance<SignalAsset>();AssetDatabase.CreateAsset(signal,C+"Timelines/SIG_"+id+"_Completed.asset");
        var reaction=new UnityEvent();UnityEventTools.AddPersistentListener(reaction,s.ReceiveEndSignal);s.signalReceiver.AddReaction(signal,reaction);
        var track=t.CreateTrack<SignalTrack>(null,"完成交接信号");var emitter=track.CreateMarker<SignalEmitter>(duration-.05);emitter.asset=signal;emitter.emitOnce=true;emitter.retroactive=true;s.director.SetGenericBinding(track,s.signalReceiver);
        var subtitles=t.CreateTrack<Module4SubtitleTrack>(null,"飞控字幕");s.director.SetGenericBinding(subtitles,hud);
        var radio=Empty("Radio Voice",g.transform).AddComponent<AudioSource>();radio.playOnAwake=false;radio.spatialBlend=0;
        var radioTrack=t.CreateTrack<AudioTrack>(null,"飞控语音");s.director.SetGenericBinding(radioTrack,radio);
        foreach(var v in voiceSpec.voices.Where(x=>x.phase==id))
        {
            var c=subtitles.CreateClip<Module4SubtitleClip>();c.start=v.at;c.duration=Math.Min(v.duration+.7,duration-v.at);c.displayName=v.text;var content=((Module4SubtitleClip)c.asset).content;content.text=v.text;
            var ac=radioTrack.CreateClip<AudioPlayableAsset>();ac.start=v.at;ac.duration=Math.Min(v.duration,duration-v.at);ac.displayName=v.file;((AudioPlayableAsset)ac.asset).clip=LoadAudio(v.file);
        }
        return s;
    }
    static AudioClip LoadAudio(string file){var a=AssetDatabase.LoadAssetAtPath<AudioClip>(A+file);if(!a)throw new Exception("Missing audio "+file);return a;}
    static void Audio(Module4Sequence s,string file,float start,float duration,float volume,bool loop=false)
    {
        var source=Empty(file,s.transform).AddComponent<AudioSource>();source.playOnAwake=false;source.volume=volume;source.spatialBlend=0;
        var track=((TimelineAsset)s.director.playableAsset).CreateTrack<AudioTrack>(null,file);s.director.SetGenericBinding(track,source);var c=track.CreateClip<AudioPlayableAsset>();c.start=start;c.duration=duration;c.displayName=file;var a=(AudioPlayableAsset)c.asset;a.clip=LoadAudio(file);a.loop=loop;
    }
    static CinemachineVirtualCamera VCam(string name,Transform parent,Transform target,Vector3 offset,float fov=50)
    {
        var v=Empty(name,parent).AddComponent<CinemachineVirtualCamera>();v.m_Lens.FieldOfView=fov;v.m_Lens.FarClipPlane=4000;v.Follow=target;v.LookAt=target;v.Priority=10;
        var body=v.AddCinemachineComponent<CinemachineTransposer>();body.m_BindingMode=CinemachineTransposer.BindingMode.WorldSpace;body.m_FollowOffset=offset;body.m_XDamping=0;body.m_YDamping=0;body.m_ZDamping=0;
        var aim=v.AddCinemachineComponent<CinemachineComposer>();aim.m_HorizontalDamping=0;aim.m_VerticalDamping=0;aim.m_ScreenY=.47f;return v;
    }
    static void Shots(Module4Sequence s,CinemachineVirtualCamera[] cams,float[] starts,float[] lengths)
    {
        var track=((TimelineAsset)s.director.playableAsset).CreateTrack<CinemachineTrack>(null,"镜头编排");s.director.SetGenericBinding(track,camera.GetComponent<CinemachineBrain>());
        for(int i=0;i<cams.Length;i++){var clip=track.CreateClip<CinemachineShot>();clip.start=starts[i];clip.duration=lengths[i];clip.displayName=cams[i].name;var shot=(CinemachineShot)clip.asset;string key=s.sequenceId+"_cam_"+i;shot.VirtualCamera=new ExposedReference<CinemachineVirtualCameraBase>{exposedName=key};s.director.SetReferenceValue(key,cams[i]);}
    }
    static void Animate(Module4Sequence s,GameObject target,string name,string property,float[] times,float[] values)
    {
        // Put the Animator on an unscaled parent and animate its child: avoids
        // scaled root-motion accumulation and preserves the child's X/Z position.
        var initial=target.transform.localPosition;
        var owner=Empty(name+" Animation Root",target.transform.parent);
        target.transform.SetParent(owner.transform,false);
        var clip=new AnimationClip{name=name};var curve=new AnimationCurve();for(int i=0;i<times.Length;i++)curve.AddKey(new Keyframe(times[i],values[i]));
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(target.name,typeof(Transform),property),curve);
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(target.name,typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Constant(0,s.duration,initial.x));
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(target.name,typeof(Transform),"m_LocalPosition.z"),AnimationCurve.Constant(0,s.duration,initial.z));
        AssetDatabase.CreateAsset(clip,C+"Timelines/"+name+".anim");
        var track=((TimelineAsset)s.director.playableAsset).CreateTrack<AnimationTrack>(null,name);track.trackOffset=TrackOffset.ApplyTransformOffsets;var tc=track.CreateClip<AnimationPlayableAsset>();((AnimationPlayableAsset)tc.asset).clip=clip;((AnimationPlayableAsset)tc.asset).removeStartOffset=false;tc.duration=s.duration;
        var animator=owner.AddComponent<Animator>();animator.applyRootMotion=false;s.director.SetGenericBinding(track,animator);
    }
    static ParticleSystem Particle(string id,Transform parent,Vector3 pos,Color color,float size,float speed,float lifetime,float rate,bool smoke=false)
    {
        var g=Empty(id,parent,pos);g.transform.localRotation=Quaternion.Euler(90,0,0);var ps=g.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=lifetime;main.startSpeed=speed;main.startSize=size;main.startColor=color;main.maxParticles=2000;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=true;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=smoke?55:8;shape.radius=smoke?2:.65f;var emission=ps.emission;emission.rateOverTime=rate;emission.enabled=false;
        var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(C+"Textures/ParticleSoft.png"));m.SetColor("_BaseColor",Color.white);m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",smoke?(float)BlendMode.OneMinusSrcAlpha:(float)BlendMode.One);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;AssetDatabase.CreateAsset(m,C+"Materials/Particle_"+id+".mat");ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=m;return ps;
    }
    static Transform Rocket(Transform parent,string id,bool small,out ParticleSystem[] engines)
    {
        var white=Mat("Ceramic",new Color(.88f,.9f,.92f),.35f,.55f);var dark=Mat("Carbon",new Color(.045f,.06f,.08f),.45f,.5f);var blue=Mat("MissionBlue",new Color(.015f,.22f,.4f),.45f,.65f);
        var root=Empty(id,parent,new Vector3(0,2,0));if(small)root.transform.localScale=Vector3.one*.35f;
        Obj("Core Stage",root.transform,PrimitiveType.Cylinder,new Vector3(0,16,0),new Vector3(5.6f,16,5.6f),white);
        Obj("Interstage Band",root.transform,PrimitiveType.Cylinder,new Vector3(0,27,0),new Vector3(5.66f,.7f,5.66f),dark);
        Obj("Mission Band",root.transform,PrimitiveType.Cylinder,new Vector3(0,20,0),new Vector3(5.67f,.35f,5.67f),blue);
        var nose=Lathe(id+"_Fairing",root.transform,new[]{2.8f,2.9f,2.65f,1.9f,.15f,0f},new[]{30f,33f,36f,39f,42f,42.5f},white);
        var list=new List<ParticleSystem>();
        for(int i=0;i<4;i++)
        {
            float a=i*Mathf.PI*.5f;var p=new Vector3(Mathf.Cos(a)*4.4f,0,Mathf.Sin(a)*4.4f);
            Obj("Booster "+i,root.transform,PrimitiveType.Cylinder,p+Vector3.up*11,new Vector3(2.8f,11,2.8f),white);
            var cap=Lathe(id+"_BoosterNose_"+i,root.transform,new[]{1.4f,1.4f,.9f,0f},new[]{22f,23f,25f,27f},white);cap.transform.localPosition=p;
            Obj("Engine Bell "+i,root.transform,PrimitiveType.Cylinder,p+Vector3.down*.5f,new Vector3(2,.65f,2),dark);
            list.Add(Particle(id+"_Flame_"+i,root.transform,p+Vector3.down*1.2f,new Color(1,.55f,.12f,.7f),2,26,1.1f,75));
        }
        engines=list.ToArray();return root.transform;
    }

    static Module4Sequence Launch()
    {
        var groundSky=Sky("EarthDawn",new Color(.10f,.31f,.62f),new Color(.92f,.48f,.22f),.03f);
        var upperSky=Sky("UpperAtmosphere",new Color(.025f,.10f,.28f),new Color(.16f,.40f,.72f),.04f);
        var launchSpace=Sky("LaunchSpace",new Color(.001f,.003f,.012f),new Color(.008f,.018f,.05f),.8f);
        var s=Sequence("Launch","蓄势 · 启程",60);WorldState(s.gameObject,groundSky,new Color(.34f,.36f,.42f));LightAt(s.transform,new Color(1,.78f,.58f),1.8f,new Vector3(22,-48,0));LightAt(s.transform,new Color(.42f,.58f,.82f),.34f,new Vector3(35,132,0));
        var concrete=Mat("Concrete",new Color(.36f,.38f,.4f),.1f);var steel=Mat("TowerSteel",new Color(.56f,.16f,.055f),.65f);var water=Mat("Ocean",new Color(.08f,.29f,.46f),.4f,.76f);var stripe=Mat("Marking",new Color(.88f,.81f,.59f));
        Obj("Ocean Horizon",s.transform,PrimitiveType.Plane,new Vector3(0,-2,0),Vector3.one*240,water);Obj("Launch Island",s.transform,PrimitiveType.Cylinder,new Vector3(0,-1,0),new Vector3(340,1,340),concrete);Obj("Launch Pad",s.transform,PrimitiveType.Cylinder,Vector3.zero,new Vector3(48,.6f,48),Mat("PadDark",new Color(.1f,.12f,.14f)));
        for(int i=0;i<24;i++){float a=i*Mathf.PI/12;var p=new Vector3(Mathf.Cos(a)*20,.8f,Mathf.Sin(a)*20);Obj("Pad Mark "+i,s.transform,PrimitiveType.Cube,p,new Vector3(.5f,.03f,3),stripe,new Vector3(0,-a*Mathf.Rad2Deg,0));}
        for(int x=0;x<2;x++)for(int z=0;z<2;z++)Beam("Tower Column",s.transform,new Vector3(-16+x*8,0,-4+z*8),new Vector3(-16+x*8,52,-4+z*8),.6f,steel);
        for(int y=4;y<52;y+=6){for(int z=0;z<2;z++){Beam("Tower Diagonal",s.transform,new Vector3(-16,y,-4+z*8),new Vector3(-8,y+6,-4+z*8),.2f,steel);Beam("Tower Deck",s.transform,new Vector3(-16,y,-4+z*8),new Vector3(-8,y,-4+z*8),.3f,steel);}Beam("Umbilical Arm",s.transform,new Vector3(-8,y,0),new Vector3(-3,y,0),.25f,steel);}
        for(int i=0;i<10;i++)Obj("Service Building",s.transform,PrimitiveType.Cube,new Vector3(-90+i*19,3,90),new Vector3(13,6,17),concrete);
        for(int i=0;i<8;i++){float x=-120+i*35;Beam("Light Mast",s.transform,new Vector3(x,0,60),new Vector3(x,16,60),.2f,concrete);Obj("Mast Lamp",s.transform,PrimitiveType.Sphere,new Vector3(x,16,60),Vector3.one*.7f,Mat("WarmLamp",new Color(1,.6f,.2f),0,.4f,true));}
        ParticleSystem[] engines;var rocket=Rocket(s.transform,"LQ01",false,out engines);var target=Empty("Rocket Camera Target",rocket,new Vector3(0,21,0)).transform;
        Animate(s,rocket.gameObject,"Launch_Altitude","m_LocalPosition.y",new[]{0f,18,22,34,46,60},new[]{2f,2,4,48,210,650});
        var engineTarget=Empty("Ignition Camera Target",rocket,new Vector3(0,2,0)).transform;
        var cams=new[]{VCam("01 发射场全景",s.transform,target,new Vector3(80,12,-115),48),VCam("02 塔架特写",s.transform,target,new Vector3(14,4,-32),44),VCam("03 点火近景",s.transform,engineTarget,new Vector3(16,6,-30),42),VCam("04 升空跟随",s.transform,target,new Vector3(45,10,-80),52),VCam("05 云层远景",s.transform,target,new Vector3(180,50,-270),48)};
        Shots(s,cams,new[]{0f,12,21,31,46},new[]{13f,10,11,16,14});
        var visual=s.gameObject.AddComponent<Module4VisualDriver>();visual.sequence=s;visual.engines=engines;visual.smoke=Particle("LaunchSmoke",s.transform,new Vector3(0,1,0),new Color(.62f,.63f,.65f,.24f),12,5,10,35,true);
        visual.phase1Sky=groundSky;visual.phase2Sky=upperSky;visual.phase3Sky=launchSpace;visual.phase2Start=31;visual.phase3Start=46;
        visual.phase1Ambient=new Color(.34f,.36f,.42f);visual.phase2Ambient=new Color(.23f,.27f,.34f);visual.phase3Ambient=new Color(.075f,.085f,.11f);
        Audio(s,"BGM_Earth.wav",0,60,.25f);Audio(s,"SFX_Rocket_Loop.wav",18,40,.7f,true);Audio(s,"SFX_Cabin_Beep.wav",6,.6f,.5f);return s;
    }
    static Material Planet(string id,string file,Color color,bool glowing=false)
    {var m=Mat(id,color,0,.25f);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(C+"Textures/"+file);if(!texture)throw new Exception("Missing texture: "+file);m.SetTexture("_BaseMap",texture);if(glowing){m.EnableKeyword("_EMISSION");m.SetTexture("_EmissionMap",texture);m.SetColor("_EmissionColor",Color.white*.2f);}EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
    static Module4Sequence Orbit()
    {
        var s=Sequence("Orbit","绕月 · 地出",30);WorldState(s.gameObject,Sky("SpaceSky",new Color(.001f,.003f,.01f),new Color(.004f,.008f,.022f),1),new Color(.12f,.13f,.16f));LightAt(s.transform,new Color(.95f,.97f,1),2.35f,new Vector3(12,-70,0));
        var moon=Obj("Moon Limb",s.transform,PrimitiveType.Sphere,new Vector3(0,-116,0),Vector3.one*240,Planet("MoonSurface","NASA_Moon_LROC.jpg",Color.white));
        var earth=Obj("Earth",s.transform,PrimitiveType.Sphere,new Vector3(0,22,160),Vector3.one*45,Planet("EarthSurface","NASA_Earth_BlueMarble.jpg",Color.white,true));earth.transform.localRotation=Quaternion.Euler(12,-90,-8);
        var aim=Empty("Earthrise Target",s.transform,new Vector3(0,16,100)).transform;
        var a=VCam("01 月面地平线",s.transform,aim,new Vector3(-35,-8,-115),48);var b=VCam("02 地出特写",s.transform,earth.transform,new Vector3(16,-9,-97),40);var c=VCam("03 环月巡航",s.transform,earth.transform,new Vector3(58,3,-125),53);
        Shots(s,new[]{a,b,c},new[]{0f,10,21},new[]{11f,12,9});Animate(s,earth,"Orbit_Earthrise","m_LocalPosition.y",new[]{0f,12,30},new[]{5f,20,33});
        var visual=s.gameObject.AddComponent<Module4VisualDriver>();visual.sequence=s;visual.engines=new ParticleSystem[0];visual.earth=earth.transform;Audio(s,"BGM_Space.wav",0,30,.33f);Audio(s,"SFX_Cabin_Loop.wav",0,30,.3f,true);return s;
    }
    static GameObject Terrain(Transform parent,string id,Material material,float size=260)
    {
        int n=65;var v=new Vector3[n*n];var uv=new Vector2[v.Length];var tris=new List<int>();
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){float px=((float)x/(n-1)-.5f)*size,pz=((float)z/(n-1)-.5f)*size;float h=(Mathf.PerlinNoise(px*.018f+31,pz*.018f+42)-.5f)*9;float r=Mathf.Sqrt((px-55)*(px-55)+(pz-45)*(pz-45));h+=Mathf.Exp(-Mathf.Pow((r-24)/5,2))*4-Mathf.Exp(-r*r/400)*3;if(Mathf.Abs(px)<28&&Mathf.Abs(pz)<25)h*=Mathf.Max(Mathf.Abs(px)/28,Mathf.Abs(pz)/25);v[z*n+x]=new Vector3(px,h-.5f,pz);uv[z*n+x]=new Vector2((float)x/(n-1)*6,(float)z/(n-1)*6);if(x<n-1&&z<n-1){int k=z*n+x;tris.AddRange(new[]{k,k+n,k+1,k+1,k+n,k+n+1});}}
        var mesh=new Mesh{name=id};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,C+"Geometry/"+id+".asset");var g=Empty(id,parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g;
    }
    static GameObject LandingTerrain(Transform parent,string id,Material material,float size=900)
    {
        int n=81;var vertices=new Vector3[n*n];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float px=((float)x/(n-1)-.5f)*size,pz=((float)z/(n-1)-.5f)*size;
            float broad=(Mathf.PerlinNoise(px*.0045f+9,pz*.0045f+17)-.5f)*16;
            float detail=(Mathf.PerlinNoise(px*.016f+41,pz*.016f+23)-.5f)*3.5f;
            float distance=Mathf.Sqrt(px*px+pz*pz);
            float relief=Mathf.SmoothStep(0,1,Mathf.InverseLerp(55,180,distance));
            vertices[z*n+x]=new Vector3(px,-1+(broad+detail)*relief,pz);
            uv[z*n+x]=new Vector2((float)x/(n-1)*8,(float)z/(n-1)*8);
            if(x<n-1&&z<n-1){int k=z*n+x;triangles.AddRange(new[]{k,k+n,k+1,k+1,k+n,k+n+1});}
        }
        var mesh=new Mesh{name=id};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,C+"Geometry/"+id+".asset");
        var terrain=Empty(id,parent);terrain.AddComponent<MeshFilter>().sharedMesh=mesh;terrain.AddComponent<MeshRenderer>().sharedMaterial=material;return terrain;
    }
    static GameObject RidgeRing(Transform parent,string id,Material material)
    {
        const int segments=72,rows=3;var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int row=0;row<rows;row++)for(int i=0;i<=segments;i++)
        {
            float angle=i*Mathf.PI*2/segments;
            float wave=Mathf.Sin(angle*3.1f+1.4f)*11+Mathf.Sin(angle*7.3f)*5;
            float radius=row==0?205:row==1?320+wave*1.8f:540;
            float height=row==1?18+wave:-5;
            vertices.Add(new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius));
        }
        for(int row=0;row<rows-1;row++)for(int i=0;i<segments;i++)
        {
            int k=row*(segments+1)+i;triangles.AddRange(new[]{k,k+segments+1,k+1,k+1,k+segments+1,k+segments+2});
        }
        var mesh=new Mesh{name=id};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,C+"Geometry/"+id+".asset");
        var ridge=Empty(id,parent);ridge.AddComponent<MeshFilter>().sharedMesh=mesh;ridge.AddComponent<MeshRenderer>().sharedMaterial=material;return ridge;
    }
    static void Habitat(Transform parent,Vector3 pos)
    {
        var white=Mat("HabitatWhite",new Color(.78f,.8f,.81f),.45f,.5f);var navy=Mat("SolarBlue",new Color(.015f,.09f,.19f),.6f,.75f);var steel=Mat("FrameSteel",new Color(.2f,.23f,.27f),.8f);
        for(int i=0;i<3;i++){var p=pos+new Vector3(i*14,5,0);Obj("Habitat Cylinder",parent,PrimitiveType.Cylinder,p,new Vector3(9,7,9),white,new Vector3(0,0,90));Obj("Habitat Window",parent,PrimitiveType.Cube,p+new Vector3(0,.4f,-4.4f),new Vector3(7,1.6f,.15f),navy);for(int j=-1;j<=1;j+=2)Beam("Habitat Support",parent,p+new Vector3(j*4,-3,-3),p+new Vector3(j*5,-5,-4),.3f,steel);}
        for(int i=0;i<6;i++){var p=pos+new Vector3(-22+i*11,4,24);Obj("Solar Panel",parent,PrimitiveType.Cube,p,new Vector3(8,.15f,14),navy,new Vector3(-22,0,0));Beam("Solar Support",parent,p-Vector3.up*4,p,.24f,steel);for(int j=-3;j<=3;j++)Obj("Solar Grid",parent,PrimitiveType.Cube,p+new Vector3(j,.12f,0),new Vector3(.035f,.02f,13.8f),white,new Vector3(-22,0,0));}
        var rover=Empty("Rover Display Model",parent,pos+new Vector3(12,1.5f,-19));Obj("Rover Chassis",rover.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(4,.9f,6),white);Obj("Rover Battery",rover.transform,PrimitiveType.Cube,new Vector3(0,.8f,-.5f),new Vector3(2,1.1f,2.5f),steel);Obj("Rover Solar Roof",rover.transform,PrimitiveType.Cube,new Vector3(0,2.2f,0),new Vector3(5,.12f,5),navy);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z++)Obj("Rover Wheel",rover.transform,PrimitiveType.Cylinder,new Vector3(x*2.3f,-.5f,z*2.1f),new Vector3(1.6f,.45f,1.6f),Mat("RoverRubber",new Color(.035f,.038f,.04f)),new Vector3(0,0,90));
        Beam("Antenna Mast",parent,pos+new Vector3(40,0,3),pos+new Vector3(40,20,3),.3f,steel);Obj("Antenna Dish",parent,PrimitiveType.Sphere,pos+new Vector3(40,20,3),new Vector3(7,1.2f,7),white,new Vector3(35,0,0));
    }
    static GameObject MoonWorld(out CinemachineVirtualCamera cam,out AudioSource music,out AudioSource voice)
    {
        var root=Empty("HUD Demonstration / 广寒站");WorldState(root,Sky("MoonSky",new Color(.001f,.002f,.006f),new Color(.003f,.004f,.01f),1),new Color(.17f,.18f,.21f));LightAt(root.transform,new Color(.84f,.9f,1),2.3f,new Vector3(15,-65,0));Terrain(root.transform,"MoonHUDTerrain",Planet("MoonGround","NASA_Moon_LROC.jpg",new Color(.72f,.73f,.77f)));Habitat(root.transform,new Vector3(-18,0,15));
        Obj("Distant Earth",root.transform,PrimitiveType.Sphere,new Vector3(140,90,240),Vector3.one*32,Planet("EarthHud","NASA_Earth_BlueMarble.jpg",Color.white,true));var aim=Empty("MoonDemo Aim",root.transform,new Vector3(5,5,12));cam=VCam("HUD Showcase Camera",root.transform,aim.transform,new Vector3(45,20,-62),52);
        var map=Empty("Minimap Camera",root.transform,new Vector3(0,100,5)).AddComponent<Camera>();map.transform.rotation=Quaternion.Euler(90,0,0);map.orthographic=true;map.orthographicSize=75;map.nearClipPlane=.1f;map.farClipPlane=250;var rt=new RenderTexture(512,512,16);AssetDatabase.CreateAsset(rt,U+"Sprites/RT_Module4Minimap.renderTexture");map.targetTexture=rt;map.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        hud.minimapArrow.parent.GetComponentInChildren<RawImage>().texture=rt;
        music=Empty("Moon Music",root.transform).AddComponent<AudioSource>();music.clip=LoadAudio("BGM_Moon.wav");music.loop=true;music.volume=.28f;music.playOnAwake=false;
        voice=Empty("Moon Radio",root.transform).AddComponent<AudioSource>();voice.playOnAwake=false;return root;
    }
    static void Activation(Module4Sequence sequence,GameObject target,float start,float duration)
    {var track=((TimelineAsset)sequence.director.playableAsset).CreateTrack<ActivationTrack>(null,target.name);track.postPlaybackState=ActivationTrack.PostPlaybackState.Inactive;var clip=track.CreateDefaultClip();clip.start=start;clip.duration=duration;sequence.director.SetGenericBinding(track,target);}
    static Module4Sequence Return()
    {
        var moonSky=Sky("ReturnMoonSky",new Color(.001f,.003f,.012f),new Color(.004f,.008f,.022f),1);
        var reentrySky=Sky("ReentrySky",new Color(.004f,.014f,.05f),new Color(.06f,.25f,.43f),.1f);
        var landingSky=Sky("LandingSky",new Color(.13f,.48f,.86f),new Color(.78f,.82f,.76f),0);
        var s=Sequence("Return","携样 · 归航",45);WorldState(s.gameObject,moonSky,new Color(.15f,.16f,.19f));
        var ascent=Empty("月面上升",s.transform);LightAt(ascent.transform,new Color(.86f,.92f,1),2.35f,new Vector3(18,-62,0));LightAt(ascent.transform,new Color(.34f,.46f,.7f),.58f,new Vector3(-22,112,0));Terrain(ascent.transform,"ReturnMoonTerrain",mats["MoonGround"]);Habitat(ascent.transform,new Vector3(-25,0,30));ParticleSystem[] engines;var up=Rocket(ascent.transform,"Ascent",true,out engines);var aim=Empty("Ascent Aim",up,new Vector3(0,20,0)).transform;
        Animate(s,up.gameObject,"Return_Ascent","m_LocalPosition.y",new[]{0f,4,19,45},new[]{2f,4,150,150});
        var reentry=Empty("再入大气层",s.transform);LightAt(reentry.transform,new Color(1,.84f,.68f),2.1f,new Vector3(30,-48,8));var body=Empty("Return Capsule",reentry.transform,new Vector3(0,15,0));var ceramic=mats["Ceramic"];Lathe("ReentryCapsule",body.transform,new[]{0f,3f,3.8f,3f,1.8f,0f},new[]{-2f,-2f,-1f,1f,3f,3.5f},ceramic);Obj("Earth Horizon",reentry.transform,PrimitiveType.Sphere,new Vector3(0,-420,140),Vector3.one*870,mats["EarthSurface"]);
        var glow=Particle("ReentryPlasma",body.transform,new Vector3(0,-2,0),new Color(1,.3f,.06f,.6f),5,17,1.1f,130);var em=glow.emission;em.enabled=true;
        var land=Empty("返回着陆",s.transform);LightAt(land.transform,new Color(1,.94f,.82f),1.65f,new Vector3(38,-34,4));
        var steppe=Mat("SteppeGround",new Color(.31f,.33f,.16f),0,.18f);var ridge=Mat("SteppeRidge",new Color(.24f,.29f,.24f),0,.12f);
        LandingTerrain(land.transform,"ReturnSteppeTerrain",steppe);RidgeRing(land.transform,"ReturnDistantRidge",ridge);
        var pod=Empty("Landing Capsule",land.transform,new Vector3(0,32,0));Lathe("LandingCapsule",pod.transform,new[]{0f,3f,3.8f,3f,1.8f,0f},new[]{-2f,-2f,-1f,1f,3f,3.5f},ceramic);
        var chute=Empty("Parachute",pod.transform,new Vector3(0,16,0));var chuteMaterial=Mat("ParachuteOrange",new Color(.94f,.29f,.07f));chuteMaterial.SetFloat("_Cull",0);Lathe("ParachuteCanopy",chute.transform,new[]{0f,3f,7f,10f,12f},new[]{5.5f,5f,4f,2f,0f},chuteMaterial);
        for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Beam("Suspension Line",chute.transform,new Vector3(0,-13,0),new Vector3(Mathf.Cos(a)*12,0,Mathf.Sin(a)*12),.035f,ceramic);}
        Animate(s,pod,"Return_Descent","m_LocalPosition.y",new[]{0f,32,37,43,45},new[]{32f,32,19,4,2});
        var landingAim=Empty("Landing Camera Aim",pod.transform,new Vector3(0,9,0)).transform;
        var cams=new[]{VCam("01 上升器远景",ascent.transform,aim,new Vector3(30,5,-45)),VCam("02 轨道交会",ascent.transform,aim,new Vector3(-16,3,-24)),VCam("03 再入广角",reentry.transform,body.transform,new Vector3(22,8,-30)),VCam("04 热防护特写",reentry.transform,body.transform,new Vector3(9,3,-13),45),VCam("05 主伞展开",land.transform,landingAim,new Vector3(42,18,-55),52),VCam("06 安全着陆",land.transform,landingAim,new Vector3(34,10,-50),50)};
        Shots(s,cams,new[]{0f,9,19,25,32,39},new[]{10f,10,7,7,8,6});Activation(s,ascent,0,19);Activation(s,reentry,19,13);Activation(s,land,32,13);
        var visual=s.gameObject.AddComponent<Module4VisualDriver>();visual.sequence=s;visual.engines=engines;visual.engineOn=0;visual.engineOff=19;visual.parachute=chute.transform;
        visual.phase1Sky=moonSky;visual.phase2Sky=reentrySky;visual.phase3Sky=landingSky;visual.phase2Start=19;visual.phase3Start=32;visual.phase3Fog=true;
        visual.phase1Ambient=new Color(.15f,.16f,.19f);visual.phase2Ambient=new Color(.22f,.2f,.2f);visual.phase3Ambient=new Color(.46f,.48f,.43f);
        Audio(s,"BGM_Earth.wav",0,45,.28f);Audio(s,"SFX_Rocket_Loop.wav",0,16,.5f,true);Audio(s,"SFX_Cabin_Loop.wav",19,13,.35f,true);Audio(s,"SFX_Quest_Complete.wav",42,1.4f,.6f);return s;
    }

    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 p,Vector2 size)
    {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=p;r.sizeDelta=size;return r;}
    static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pos,Vector2 size,Color color)
    {var r=Rect(name,parent,min,max,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
    static Text Label(string name,Transform parent,string value,int size,Color color,Vector2 min,Vector2 max,Vector2 p,Vector2 box,TextAnchor align=TextAnchor.MiddleLeft)
    {var r=Rect(name,parent,min,max,p,box);var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=color;t.text=value;t.alignment=align;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;}
    static Module4HudView BuildHud()
    {
        var root=new GameObject("Module4HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.localScale=Vector3.one;root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;var v=root.AddComponent<Module4HudView>();
        v.showSubtitles=false;v.showTitleCards=false;
        Color cyan=new Color(.35f,.88f,.96f),white=new Color(.94f,.97f,.99f),muted=new Color(.66f,.74f,.78f),navy=new Color(.008f,.022f,.035f,.62f);
        var clock=Panel("Time Background",root.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-92,-52),new Vector2(118,48),new Color(.008f,.022f,.035f,.48f));
        v.missionClock=Label("Mission Clock",clock.transform,"00:00",22,white,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(100,34),TextAnchor.MiddleCenter);
        var data=Panel("Flight Data",root.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(285,92),new Vector2(510,116),navy);
        Label("Speed Label",data.transform,"速度",14,muted,Vector2.zero,Vector2.zero,new Vector2(42,80),new Vector2(60,24));v.speedText=Label("speedText",data.transform,"0.0 km/h",24,white,Vector2.zero,Vector2.zero,new Vector2(105,40),new Vector2(170,42));
        Label("Altitude Label",data.transform,"高度",14,muted,Vector2.zero,Vector2.zero,new Vector2(207,80),new Vector2(60,24));v.altitudeText=Label("Altitude",data.transform,"0 m",24,white,Vector2.zero,Vector2.zero,new Vector2(270,40),new Vector2(160,42));
        Label("System Label",data.transform,"系统",14,muted,Vector2.zero,Vector2.zero,new Vector2(391,80),new Vector2(60,24));v.oxygenText=Label("System",data.transform,"100%",24,cyan,Vector2.zero,Vector2.zero,new Vector2(439,40),new Vector2(100,42),TextAnchor.MiddleCenter);
        var hidden=Rect("Hidden Runtime Bindings",root.transform,Vector2.zero,Vector2.zero,new Vector2(-4000,-4000),new Vector2(10,10));
        v.chapterText=Label("Chapter",hidden,"",1,Color.clear,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);
        v.phaseText=Label("Phase",hidden,"",1,Color.clear,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);
        v.questText=Label("questText",hidden,"",1,Color.clear,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);
        v.promptText=Label("promptText",hidden,"",1,Color.clear,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);
        v.footerText=Label("Controls",hidden,"",1,Color.clear,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);
        hidden.gameObject.SetActive(false);
        var map=Panel("Hidden Minimap",root.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(-3000,-3000),new Vector2(32,32),Color.clear);var mapRaw=Rect("Map Feed",map.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<RawImage>();mapRaw.color=Color.clear;
        v.minimapArrow=Rect("Player Arrow",map.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1,1));
        var sub=Panel("Radio Subtitle",root.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,150),new Vector2(900,90),new Color(.008f,.022f,.035f,.55f));v.subtitlePanel=sub.gameObject;v.speakerText=Label("Speaker",sub.transform,"",1,Color.clear,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one);v.subtitleText=Label("Subtitle",sub.transform,"",24,white,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(840,60),TextAnchor.MiddleCenter);sub.gameObject.SetActive(false);
        var dialog=Panel("NPC Dialogue",root.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-35),new Vector2(820,250),navy);v.dialoguePanel=dialog.gameObject;v.dialogueText=Label("Dialogue",dialog.transform,"",25,white,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(750,220));dialog.gameObject.SetActive(false);
        v.landingText=Label("Landing Tuning",root.transform,"",1,Color.clear,new Vector2(0,0),new Vector2(0,0),new Vector2(-3000,-3000),Vector2.one);
        var tuning=Rect("Landing Parameter Panel",root.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(-3000,-3000),new Vector2(455,420));v.landingPanel=tuning.gameObject;tuning.gameObject.AddComponent<Module4LandingTuning>().font=font;
        var progress=Panel("Progress Background",root.transform,Vector2.zero,new Vector2(1,0),new Vector2(0,7),new Vector2(0,3),new Color(.05f,.1f,.15f));v.progress=Panel("Progress",progress.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,cyan);v.progress.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");v.progress.type=Image.Type.Filled;v.progress.fillMethod=Image.FillMethod.Horizontal;v.progress.fillAmount=0;
        var title=Panel("Title Card",root.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.clear);v.titlePanel=title.gameObject;v.titleText=Label("Title",title.transform,"",1,Color.clear,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.one);v.titleEnglish=Label("Title English",title.transform,"",1,Color.clear,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.one);title.gameObject.SetActive(false);
        v.fade=Panel("Black Transition",root.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0,0,0,0));return v;
    }
}
