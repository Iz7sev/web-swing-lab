using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace WebSwing.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("WebSwing/Setup Quest Project")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/XR");
            PlayerSettings.companyName="WebSwingLab"; PlayerSettings.productName="Web Swing Lab";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.webswinglab.questprototype");
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
            Time.fixedDeltaTime=1f/90f;
            var serialized=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=serialized.FindProperty("activeInputHandler");
            if (input!=null) { input.intValue=1; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            var perTarget=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/Settings.asset");
            if (perTarget==null)
            {
                perTarget=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget,"Assets/XR/Settings.asset");
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,perTarget,true);
            var general=perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general==null)
            {
                general=ScriptableObject.CreateInstance<XRGeneralSettings>(); general.name="Android XR";
                var manager=ScriptableObject.CreateInstance<XRManagerSettings>(); manager.name="Android XR Manager";
                general.Manager=manager;
                AssetDatabase.AddObjectToAsset(general,perTarget); AssetDatabase.AddObjectToAsset(manager,perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android,general);
            }
            XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Android);
            var openxr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openxr!=null)
            {
                foreach (var feature in openxr.GetFeatures<OpenXRFeature>())
                {
                    string type=feature.GetType().Name;
                    if (type=="HandTracking" || type=="MetaQuestFeature") feature.enabled=true;
                }
                EditorUtility.SetDirty(openxr);
            }
            EditorUtility.SetDirty(perTarget); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/WebSwingLab.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/WebSwingLab.unity",true)};
            AssetDatabase.SaveAssets();
            Debug.Log("WEB_SETUP_OK: Android OpenXR configured. Verify Hand Tracking Subsystem and Meta Quest Support in Project Settings before device build.");
        }

        [MenuItem("WebSwing/Run Physics Checks")]
        public static void Check()
        {
            Vector3 velocity=new Vector3(2,3,0);
            Vector3 position=WebPhysics.Constrain(new Vector3(8,0,0),Vector3.zero,5,ref velocity);
            Require(Mathf.Abs(position.magnitude-5)<.0001f,"Rope length");
            Require(Mathf.Abs(velocity.x)<.0001f && Mathf.Abs(velocity.y-3)<.0001f,"Tangential momentum");
            velocity=new Vector3(-2,3,0);
            WebPhysics.Constrain(new Vector3(8,0,0),Vector3.zero,5,ref velocity);
            Require(velocity.x == -2,"Inward velocity must survive");
            velocity=Vector3.one;
            position=WebPhysics.Constrain(new Vector3(2,0,0),Vector3.zero,5,ref velocity);
            Require(position.x==2 && velocity==Vector3.one,"Slack rope must be free");
            var acceleration=WebPhysics.PullAcceleration(Vector3.zero,Vector3.zero,Vector3.right*100,Vector3.zero,20,8,35);
            Require(Mathf.Abs(acceleration.magnitude-35)<.0001f,"Pull force cap");
            acceleration=WebPhysics.PullAcceleration(Vector3.zero,Vector3.right*10,Vector3.zero,Vector3.zero,20,8,35);
            Require(acceleration.x<0,"Pull damping");
            velocity=new Vector3(2,3,0);
            WebPhysics.Constrain(new Vector3(8,0,0),Vector3.zero,5,ref velocity,4);
            Require(Mathf.Abs(velocity.x+4)<.0001f && Mathf.Abs(velocity.y-3)<.0001f,"Reeling preserves tangential momentum");
            var anchor=new Vector3(0,20,0);
            position=Vector3.zero; velocity=Vector3.zero; float length=20, dt=1f/90;
            for (int i=0;i<180;i++)
            {
                velocity+=Vector3.down*9.81f*dt; length-=3f*dt;
                position=WebPhysics.Constrain(position+velocity*dt,anchor,length,ref velocity,3);
            }
            Require(position.y>5.5f && position.y<6.5f,"Web lifts player against gravity");
            float releaseHeight=position.y, releaseVelocity=velocity.y;
            for (int i=0;i<180;i++) { velocity+=Vector3.down*9.81f*dt; position+=velocity*dt; }
            Require(releaseVelocity>0 && velocity.y<0 && position.y<releaseHeight,"Release keeps upward momentum then falls under gravity");
            var force=WebPhysics.TetherForce(Vector3.right*3,Vector3.zero,Vector3.zero,Vector3.zero,4,85,20,150);
            Require(force==Vector3.zero,"Slack web cannot push an object");
            force=WebPhysics.TetherForce(Vector3.right*10,Vector3.zero,Vector3.zero,Vector3.zero,1,85,20,150);
            Require(Mathf.Abs(force.x+150)<.001f,"Object tether force cap and direction");
            force=WebPhysics.TetherForce(Vector3.right*2,Vector3.left*20,Vector3.zero,Vector3.zero,1,85,20,150);
            Require(force==Vector3.zero,"Approaching cargo is never pushed away");
            Debug.Log("WEB_PHYSICS_CHECKS_OK: 12 assertions passed");
        }
        static void Require(bool condition,string label) { if (!condition) throw new Exception(label); }
        static void ConfigureMixedReality()
        {
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            int required=0;
            foreach(var feature in settings.GetFeatures<OpenXRFeature>())
            {
                string name=feature.GetType().Name;
                if(name=="ARCameraFeature" || name=="ARSessionFeature") { feature.enabled=true; required++; }
                if(name=="OpenXRLifeCycleFeature" || name=="OpenXRCompositionLayersFeature" || name=="HandTracking" || name=="MetaQuestFeature") feature.enabled=true;
                EditorUtility.SetDirty(feature);
            }
            Require(required==2,"Meta OpenXR camera and session features must be installed");
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            Debug.Log("WEB_MR_FEATURES_OK: Meta passthrough, session, lifecycle and composition layers configured.");
        }
        public static void Batch() { Setup(); Check(); }

        [MenuItem("WebSwing/Preview Cargo Pull")]
        public static void PreviewCargoPull()
        {
            if(!EditorApplication.isPlaying) throw new Exception("Enter Play mode first.");
            var world=UnityEngine.Object.FindFirstObjectByType<DemoWorld>();
            if(world==null) throw new Exception("Demo world is not ready.");
            Rigidbody cargo=null;
            foreach(var body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if(body.name=="Cargo crate / 2 kg") cargo=body;
            if(cargo==null) throw new Exception("2kg cargo is missing.");
            world.input.enabled=false;
            world.input.aim.position=world.motor.transform.position+new Vector3(.15f,1.25f,0);
            world.input.aim.rotation=Quaternion.LookRotation(cargo.worldCenterOfMass-world.input.aim.position);
            float initialDistance=Vector3.Distance(cargo.position,world.input.aim.position);
            if(!world.shooter.Fire(2)) { world.input.enabled=true; throw new Exception("Cargo aim failed."); }
            double started=EditorApplication.timeSinceStartup;
            Vector3 startingHand=world.input.aim.position;
            Vector3 pullingDirection=(cargo.worldCenterOfMass-startingHand).normalized;
            float initialLength=world.shooter.ObjectRopeLength;
            bool stationary=true;
            EditorApplication.CallbackFunction finish=null;
            finish=()=>
            {
                if(!EditorApplication.isPlaying || world==null) { EditorApplication.update-=finish; return; }
                double elapsed=EditorApplication.timeSinceStartup-started;
                if(elapsed<1) { stationary &= Mathf.Abs(world.shooter.ObjectRopeLength-initialLength)<.001f; return; }
                world.input.aim.position=startingHand-pullingDirection*Mathf.Clamp01((float)(elapsed-1))*1.3f;
                if(elapsed<4) return;
                float distance=Vector3.Distance(cargo.position,world.input.aim.position);
                bool success=stationary && distance<initialDistance-1 && world.shooter.Active;
                world.shooter.Release();
                world.motor.Attach(new Vector3(-15,30,10),0); world.motor.Attach(new Vector3(15,30,10),1);
                bool dual=world.motor.IsAttached(0)&&world.motor.IsAttached(1);
                world.motor.ReleaseTether(0); dual &= !world.motor.IsAttached(0)&&world.motor.IsAttached(1); world.motor.Release();
                world.modes.SelectMode(true);
                bool mr=world.motor.MovementLocked && !CityEnvironment.Root.gameObject.activeSelf && world.input.head.GetComponent<Camera>().backgroundColor.a==0;
                world.modes.SelectMode(false);
                mr &= !world.motor.MovementLocked && CityEnvironment.Root.gameObject.activeSelf;
                success &= dual && mr;
                string result=(success?"PASS":"FAIL")+": stationary cargo has no auto reel; manual hand pull "+initialDistance.ToString("F2")+"m to "+distance.ToString("F2")+"m; independent dual ropes="+dual+"; MR/VR state="+mr;
                File.WriteAllText("ProjectSettings/CargoPullCheck.txt",result);
                world.shooter.Release(); world.input.enabled=true; world.modes.ShowMenu(); EditorApplication.update-=finish;
                if(success) Debug.Log("WEB_CARGO_CHECK_OK: "+result); else Debug.LogError(result);
            };
            EditorApplication.update+=finish;
        }

        [MenuItem("WebSwing/Prepare ASCII Build Copy")]
        public static void PrepareAsciiBuildCopy()
        {
            const string destination="C:/Users/Public/Documents/WebSwingQuest";
            CopyDirectory("Assets",Path.Combine(destination,"Assets"));
            CopyDirectory("Packages",Path.Combine(destination,"Packages"));
            CopyDirectory("ProjectSettings",Path.Combine(destination,"ProjectSettings"));
            File.WriteAllText(Path.Combine(destination,"ProjectSettings/BuildOnNextOpen.txt"),"Requested by this setup session");
            EditorApplication.OpenProject(destination);
        }
        static void CopyDirectory(string source,string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file,Path.Combine(destination,Path.GetFileName(file)),true);
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory,Path.Combine(destination,Path.GetFileName(directory)));
        }

        [MenuItem("WebSwing/Build And Run Quest")]
        public static void BuildQuest()
        {
            if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new Exception("Select Android as active build target first.");
            Check();
            CheckGroundTargets();
            ConfigureAppIcon();
            ConfigureMixedReality();
            PrepareRuntimeResources();
            PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.Activity;
            PlayerSettings.Android.optimizedFramePacing=false;
            Directory.CreateDirectory("Builds");
            // Android's generated prefab command files require an ASCII cache path as well.
            string gradleHome=Path.GetFullPath("BuildCache/Gradle");
            Directory.CreateDirectory(gradleHome);
            Environment.SetEnvironmentVariable("GRADLE_USER_HOME",gradleHome);
            UnityEditor.Android.AndroidExternalToolsSettings.Gradle.userHomePath=gradleHome;
            string temporaryPath=Path.GetFullPath("BuildCache/Temp");
            Directory.CreateDirectory(temporaryPath);
            Environment.SetEnvironmentVariable("TMP",temporaryPath);
            Environment.SetEnvironmentVariable("TEMP",temporaryPath);
            string javaOptions=Environment.GetEnvironmentVariable("JAVA_TOOL_OPTIONS") ?? "";
            if (!javaOptions.Contains("-Djava.io.tmpdir="))
                Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS",javaOptions+" -Djava.io.tmpdir="+temporaryPath.Replace('\\','/'));
            File.WriteAllText("ProjectSettings/WebSwingBuildResult.txt","Building APK...");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Scenes/WebSwingLab.unity"},
                locationPathName="Builds/WebSwingLab.apk",
                target=BuildTarget.Android,
                options=BuildOptions.AutoRunPlayer
            });
            string result=report.summary.result+"; errors="+report.summary.totalErrors+"; time="+report.summary.totalTime;
            File.WriteAllText("ProjectSettings/WebSwingBuildResult.txt",result);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Quest build failed: "+result);
            Debug.Log("WEB_QUEST_BUILD_OK: "+result);
        }

        static void ConfigureAppIcon()
        {
            const string folder="Assets/WebSwing/Art/Icons/";
            foreach(string name in new[]{"WebSwingIcon", "WebSwingForeground", "WebSwingBackground"})
            {
                string path=folder+name+".png";
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency=true;
                importer.maxTextureSize=1024;
                importer.SaveAndReimport();
            }
            var mark=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"WebSwingIcon.png");
            var foreground=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"WebSwingForeground.png");
            var background=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"WebSwingBackground.png");
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{mark});
            foreach(var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
                foreach(var icon in icons)
                {
                    if(icon.minLayerCount>1) icon.SetTextures(new[]{background,foreground});
                    else icon.SetTexture(mark);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,kind,icons);
            }
            PlayerSettings.bundleVersion="1.1";
            PlayerSettings.Android.bundleVersionCode=2;
            AssetDatabase.SaveAssets();
            Debug.Log("WEB_APP_ICON_OK: web mark configured for Android launcher icons.");
        }

        static void CheckGroundTargets()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var aim=new GameObject("Temporary ground aim check");
            try
            {
                floor.transform.position=new Vector3(10000,-1,0);
                floor.transform.localScale=new Vector3(10,2,10);
                aim.transform.position=new Vector3(10000,3,0);
                aim.transform.rotation=Quaternion.LookRotation(Vector3.down);
                var shooter=aim.AddComponent<WebShooter>(); shooter.hand=aim.transform;
                Physics.SyncTransforms();
                Require(shooter.Aim(out var hit) && hit.collider==floor.GetComponent<Collider>(),"Layer zero ground accepts web");
                aim.transform.position=new Vector3(10000,-1,-10);
                aim.transform.rotation=Quaternion.LookRotation(Vector3.forward);
                Require(!shooter.Aim(out _),"Non-target vertical wall blocks web");
                floor.layer=8;
                Require(shooter.Aim(out _),"Designated web wall remains attachable");
                Require(InteractionMotion.WalkTarget(Vector3.forward,Vector3.back,Vector3.forward)>3,"Normal arm swing walks faster");
                Require(InteractionMotion.WalkTarget(Vector3.forward*10,Vector3.back*10,Vector3.forward)<=4.5f,"New walking speed cap");
                Require(InteractionMotion.WalkTarget(Vector3.zero,Vector3.zero,Vector3.forward)==0,"Stationary hands do not walk");
                Debug.Log("WEB_GROUND_WALK_CHECK_OK: ground, wall blocking, wall targets and faster arm walking passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(aim); UnityEngine.Object.DestroyImmediate(floor); }
        }

        static void PrepareRuntimeResources()
        {
            Directory.CreateDirectory("Assets/Resources/WebSwing");
            const string surfaceAtlas="Assets/Resources/WebSwing/CitySurfaces.png";
            AssetDatabase.ImportAsset(surfaceAtlas);
            var atlasImporter=AssetImporter.GetAtPath(surfaceAtlas) as TextureImporter;
            if(atlasImporter!=null && (!atlasImporter.isReadable || atlasImporter.textureCompression!=TextureImporterCompression.Uncompressed))
            {
                atlasImporter.isReadable=true;
                atlasImporter.textureCompression=TextureImporterCompression.Uncompressed;
                atlasImporter.maxTextureSize=1024;
                atlasImporter.SaveAndReimport();
            }
            foreach (var primitiveType in new[]{PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule})
            {
                var primitive=GameObject.CreatePrimitive(primitiveType);
                try { PrefabUtility.SaveAsPrefabAsset(primitive,"Assets/Resources/WebSwing/"+primitiveType+".prefab"); }
                finally { UnityEngine.Object.DestroyImmediate(primitive); }
            }
            foreach (string shaderName in new[]{"Standard", "Sprites/Default", "Skybox/Procedural", "Unlit/Color"})
            {
                var shader=Shader.Find(shaderName);
                if (shader==null) throw new Exception("Missing required shader: "+shaderName);
                string path="Assets/Resources/WebSwing/"+shaderName.Replace('/','_')+".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path)==null)
                    AssetDatabase.CreateAsset(new Material(shader),path);
            }
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var fontObject=new GameObject("Runtime font reference");
            try
            {
                fontObject.AddComponent<TextMesh>().font=font;
                PrefabUtility.SaveAsPrefabAsset(fontObject,"Assets/Resources/WebSwing/RuntimeFont.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(fontObject); }
            AssetDatabase.SaveAssets();
        }
    }
}

