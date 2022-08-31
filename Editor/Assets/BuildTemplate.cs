using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SmartFormat;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BuildFrontend
{
    public class BuildTemplate : BuildFrontendAssetBase
    {
        [Header("Configuration")]
        public BuildProfile Profile;
        public SceneList SceneList;
        public ScriptingDefineList ScriptingDefineList;
        public ScriptDefine[] EnabledScriptDefines;

        [Header("Output Options")]
        public string BuildPath;
        public string ExecutableName;

        [Header("Build/Run Options")]
        public bool CleanupBeforeBuild = true;
        public bool OpenInExplorer;

        public string RunWithArguments;

        public BuildProcessor[] Processors;

        [Serializable]
        public struct ScriptDefine
        {
            public bool Enable;
            public string DefineName;
        }

        public bool BuildEnabled
        {
            get => EditorPrefs.GetBool(PreferenceName, true);
            set => EditorPrefs.SetBool(PreferenceName, value);
        }

        string PreferenceName => $"BuildFrontend.{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this))}.Enabled";

        // =================================================================================

        protected override void Awake()
        {
            base.Awake();

            BuildPath ??= "Build/";
        }

        public BuildReport DoBuild(bool run = false)
        {
            if (!BuildEnabled)
            {
                Debug.LogWarning("Build is disabled");
                return null;
            }

            BuildReport report = null;

            try
            {
                bool prevCopyPdbFiles = UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles;

                if (Processors != null)
                {
                    foreach (var processor in Processors)
                    {
                        if (processor == null)
                            continue;

                        EditorUtility.DisplayProgressBar("Build Frontend", $"Pre-Processing : {processor.name}",
                            0.0f);
                        if (!processor.OnPreProcess(this, run))
                        {
                            throw new BuildProcessorException(processor, this);
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Build Frontend", $"Building player : {name}", 0.0f);

                var buildOptions = new BuildPlayerOptions();

                if (Profile == null)
                {
                    Debug.LogError($"Aborting build for {name}. No Build Profile is assigned.", this);
                    return null;
                }

                if (SceneList == null)
                {
                    Debug.LogError($"Aborting build for {name}. No Scene List is assigned.", this);
                    return null;
                }

                // -----------------------

                buildOptions.options = BuildOptions.None;
                if (Profile.DevelopmentBuild)
                {
                    buildOptions.options |= BuildOptions.Development;
                    if (Profile.AutoConnectProfiler)
                    {
                        buildOptions.options |= BuildOptions.ConnectWithProfiler;
                    }

                    if (Profile.DeepProfiling)
                    {
                        buildOptions.options |= BuildOptions.EnableDeepProfilingSupport;
                    }

                    if (Profile.AllowScriptDebugging)
                    {
                        buildOptions.options |= BuildOptions.AllowDebugging;
                    }

                    if (Profile.ShaderLiveLink)
                    {
                        buildOptions.options |= BuildOptions.ShaderLivelinkSupport;
                    }

                    UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles = Profile.CopyPDBFiles;
                }
                else
                {
                    UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles = false;
                }

                if (Profile.DetailedBuildReport)
                {
                    buildOptions.options |= BuildOptions.DetailedBuildReport;
                }

                switch (Profile.CompressionMethod)
                {
                    case BuildProfile.BuildCompressionMethod.LZ4:
                        buildOptions.options |= BuildOptions.CompressWithLz4;
                        break;
                    case BuildProfile.BuildCompressionMethod.LZ4HighCompression:
                        buildOptions.options |= BuildOptions.CompressWithLz4HC;
                        break;
                }

                // -----------------------

                var scriptingDefines = new List<string>();
                for (int n = 0, len = EnabledScriptDefines.Length; n < len; ++n)
                {
                    if (!EnabledScriptDefines[n].Enable)
                    {
                        continue;
                    }

                    scriptingDefines.Add(EnabledScriptDefines[n].DefineName);
                }

                buildOptions.extraScriptingDefines = scriptingDefines.ToArray();

                // -----------------------

                buildOptions.target = Profile.Target;
                buildOptions.targetGroup = BuildPipeline.GetBuildTargetGroup(Profile.Target);

                buildOptions.subtarget = 0; //todo look into this

                // -----------------------

                string buildPath;
                try
                {
                    buildPath = BuildFullPath;
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    return null;
                }

                if (CleanupBeforeBuild && Directory.Exists(buildPath))
                {
                    EditorUtility.DisplayProgressBar("Build Frontend", $"Cleaning up folder : {buildPath}", 0.05f);

                    Directory.Delete(buildPath, true);
                    Directory.CreateDirectory(buildPath);
                }

                buildOptions.locationPathName = Path.Combine(buildPath, ExecutableName);

                // -----------------------

                buildOptions.scenes = SceneList.ScenePaths;

                if (buildOptions.scenes.Length == 0)
                {
                    Debug.LogError($"Aborting build for {name}. No usable scenes could be included from {SceneList.name}.", SceneList);
                    return null;
                }

                // -----------------------

                report = BuildPipeline.BuildPlayer(buildOptions);

                // -----------------------

                if (Processors != null)
                {
                    foreach (var processor in Processors)
                    {
                        if (processor == null)
                            continue;

                        EditorUtility.DisplayProgressBar("Build Frontend", $"Post-Processing : {processor.name}",
                            0.0f);
                        if (!processor.OnPostProcess(this, report, run))
                        {
                            throw new BuildProcessorException(processor, this);
                        }
                    }
                }

                if (run)
                {
                    if (report.summary.result == BuildResult.Succeeded ||
                        EditorUtility.DisplayDialog("Run Failed Build",
                            "The build has failed or has been canceled, do you want to attempt to run previous build instead?",
                            "Yes", "No"))
                    {
                        RunBuild();
                    }
                }

                UnityEditor.WindowsStandalone.UserBuildSettings.copyPDBFiles = prevCopyPdbFiles;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return report;
        }

        // =================================================================================

        public static bool FilePathHasInvalidChars(string path)
        {
            return (!string.IsNullOrEmpty(path) && path.IndexOfAny(Path.GetInvalidPathChars()) >= 0);
        }

        struct BuildPathArgs
        {
            public string ProjectName;
            public string ProjectVersion;
            public string UnityVersion;
            public string BuildTemplateName;
            public DateTime DateTimeNow;
            public int Counter;
        }

        public string BuildFullPath
        {
            get
            {
                var args = new BuildPathArgs
                {
                    ProjectName = Application.productName,
                    ProjectVersion = Application.version,
                    UnityVersion = Application.unityVersion,
                    BuildTemplateName = name,
                    DateTimeNow = DateTime.Now,
                    Counter = 1,
                };
                string finalBuildPath = Smart.Format(BuildPath, args);

                if (FilePathHasInvalidChars(finalBuildPath))
                {
                    return finalBuildPath;
                }

                string finalFullPath = Path.GetFullPath(Path.Combine(Path.Combine(Application.dataPath, ".."), finalBuildPath));

                if (BuildPath.Contains("{Counter"))
                {
                    // increase counter if folder already exists
                    while (Directory.Exists(finalFullPath))
                    {
                        args.Counter += 1;
                        finalBuildPath = Smart.Format(BuildPath, args);
                        finalFullPath = Path.GetFullPath(Path.Combine(Path.Combine(Application.dataPath, ".."), finalBuildPath));
                    }
                }

                return finalFullPath;
            }
        }

        public bool BuildExecutableExists
        {
            get
            {
                string buildPath;
                try
                {
                    buildPath = BuildFullPath;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return false;
                }
                return File.Exists(Path.Combine(buildPath, ExecutableName));
            }
        }

        /// <summary>
        /// Returns false for builds that we can't run in this PC (mobile, console, standalone builds for a different OS, etc.).
        /// </summary>
        public bool CanRunFromEditor
        {
            get
            {
                if (Profile == null) return false;
#if UNITY_EDITOR_WIN
                return Profile.Target is BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows;
#elif UNITY_EDITOR_OSX
                return Profile.Target == BuildTarget.StandaloneOSX;
#elif UNITY_EDITOR_LINUX
                return Profile.Target == BuildTarget.StandaloneLinux64;
#else
                return false;
#endif
            }
        }

        public static void OpenBuild(string path)
        {
            var info = new ProcessStartInfo();
            path = $"\"{path}\"";

#if UNITY_EDITOR_WIN
            info.FileName = "explorer.exe";
            path = path.Replace("/", "\\");
            info.Arguments = $"/root,{path}";
#elif UNITY_EDITOR_OSX
            info.FileName = "open";
            path = path.Replace("\\", "/");
            info.Arguments = $"{path}";
#elif UNITY_EDITOR_LINUX
            info.FileName = "nautilus";
            path = path.Replace("\\", "/");
            info.Arguments = $"{path}";
#else
            // unknown OS
            return;
#endif

            Process.Start(info);
        }

        public static void RunBuild(string path, string args = null)
        {
            var info = new ProcessStartInfo();
            info.FileName = path;
            info.Arguments = string.IsNullOrEmpty(args) ? string.Empty : args;

            var parentDir = Directory.GetParent(path);
            string parentPath = parentDir?.FullName;

            info.WorkingDirectory = string.IsNullOrEmpty(parentPath) ? string.Empty : parentPath;
            info.UseShellExecute = false;

            EditorUtility.DisplayProgressBar("Build Frontend", $"Running Player : {info.FileName}", 1.0f);
            Process.Start(info);
            EditorUtility.ClearProgressBar();
        }

        public void RunBuild()
        {
            bool canRun = Profile != null && !OpenInExplorer && CanRunFromEditor;

            string path = BuildFullPath;

            if (canRun)
            {
                var info = new ProcessStartInfo();
                info.FileName = Path.Combine(path, ExecutableName);
                info.Arguments = RunWithArguments;
                info.WorkingDirectory = path;
                info.UseShellExecute = false;

                EditorUtility.DisplayProgressBar("Build Frontend", $"Running Player : {info.FileName}", 1.0f);
                Process.Start(info);
                EditorUtility.ClearProgressBar();
            }
            else
            {
                OpenBuild(path);
            }
        }
    }

}