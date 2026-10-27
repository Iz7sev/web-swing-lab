using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WebSwing.Editor
{
    // One-time setup for this new prototype, including a read-only USB device report.
    public static class FirstLaunchSetup
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.delayCall += Run;
        }
        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Run; return; }
            if (File.Exists("ProjectSettings/BuildOnNextOpen.txt") && !SessionState.GetBool("WebSwing.BuildStarted",false))
            {
                SessionState.SetBool("WebSwing.BuildStarted",true);
                File.Move("ProjectSettings/BuildOnNextOpen.txt","ProjectSettings/BuildRequestStarted.txt");
                EditorApplication.delayCall += () =>
                {
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
                        EditorApplication.delayCall += ProjectSetup.BuildQuest;
                    }
                    else ProjectSetup.BuildQuest();
                };
                return;
            }
            if (File.Exists("ProjectSettings/WebSwingSetupDone.txt")) return;
            if (SessionState.GetBool("WebSwing.SetupAttempted",false)) return;
            SessionState.SetBool("WebSwing.SetupAttempted",true);
            try
            {
                ProjectSetup.Batch();
                File.WriteAllText("ProjectSettings/WebSwingSetupDone.txt","Setup completed at "+DateTime.Now.ToString("s"));
                QueryDevices();
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); File.WriteAllText("ProjectSettings/WebSwingSetupError.txt",ex.ToString()); }
        }
        [MenuItem("WebSwing/Check Quest USB Connection")]
        public static void QueryDevices()
        {
            string adb=Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe");
            if (!File.Exists(adb)) { File.WriteAllText("ProjectSettings/QuestConnection.txt","Android Build Support / adb is not installed."); return; }
            var process=new Process();
            process.StartInfo=new ProcessStartInfo(adb,"devices -l")
            { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
            process.Start();
            // Nonblocking output collection avoids blocking the Editor on a device authorization prompt.
            var output=process.StandardOutput.ReadToEndAsync();
            var errors=process.StandardError.ReadToEndAsync();
            EditorApplication.CallbackFunction poll=null;
            poll=()=>
            {
                if (!process.HasExited || !output.IsCompleted || !errors.IsCompleted) return;
                EditorApplication.update-=poll;
                File.WriteAllText("ProjectSettings/QuestConnection.txt",output.Result+"\n"+errors.Result);
                UnityEngine.Debug.Log("Quest USB device report saved: ProjectSettings/QuestConnection.txt");
                process.Dispose();
            };
            EditorApplication.update+=poll;
        }
    }
}
