using System;
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
        public bool BuildEnabled
        {
            get { return EditorPrefs.GetBool(preferenceName, true); }
            set { EditorPrefs.SetBool(preferenceName, value); }
        }

        string preferenceName
        {
            get { return $"BuildFrontend.{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this))}.Enabled"; }
        }

        [Header("Configuration")]
        public BuildProfile Profile;
        public SceneList SceneList;

        [Header("Output Options")]
        public string BuildPath;
        public string ExecutableName;

        [Header("Build/Run Options")]
        public bool CleanupBeforeBuild = true;
        public bool OpenInExplorer;
        public string RunWithArguments;
        public BuildProcessor[] processors;

        protected override void Awake()
        {
            base.Awake();

            if (BuildPath == null)
                BuildPath = "Build/";
        }

        struct BuildPathArgs
        {
            public string ProjectName;
            public string BuildTemplateName;
            public DateTime DateTimeNow;
            public int Counter;
        }

        public BuildReport DoBuild(bool run = false)
        {
            BuildReport report = null;

            if (BuildEnabled)
            {
                try
                {
                    if(processors != null)
                    {
                        foreach(var processor in processors)
                        {
                            if (processor == null)
                                continue;

                            EditorUtility.DisplayProgressBar("Build Frontend", $"Pre-Processing : {processor.name}", 0.0f);
                            if(!processor.OnPreProcess(this, run))
                            {
                                throw new BuildProcessorException(processor, this);
                            }
                        }
                    }

                    EditorUtility.DisplayProgressBar("Build Frontend", $"Building player : {name}", 0.0f);

                    BuildOptions options = BuildOptions.None;

                    if (Profile.DevPlayer)
                        options |= BuildOptions.Development;

                    string buildPath;
                    try
                    {
                        buildPath = buildFullPath;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        return null;
                    }

                    if (CleanupBeforeBuild && Directory.Exists(buildPath))
                    {
                        EditorUtility.DisplayProgressBar("Build Frontend", $"Cleaning up folder : {buildPath}", 0.05f);

                        Directory.Delete(buildPath, true);
                        Directory.CreateDirectory(buildPath);
                    }

                    report = BuildPipeline.BuildPlayer(SceneList.scenePaths, buildPath + ExecutableName, Profile.Target, options);

                    if (processors != null)
                    {
                        foreach (var processor in processors)
                        {
                            if (processor == null)
                                continue;

                            EditorUtility.DisplayProgressBar("Build Frontend", $"Post-Processing : {processor.name}", 0.0f);
                            if (!processor.OnPostProcess(this, run))
                            {
                                throw new BuildProcessorException(processor, this);
                            }
                        }
                    }

                    if (run)
                    {
                        if (
                            report.summary.result == BuildResult.Succeeded ||
                            EditorUtility.DisplayDialog("Run Failed Build", "The build has failed or has been canceled, do you want to attempt to run previous build instead?", "Yes", "No")
                          )
                        {
                            RunBuild();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            else
            {
                Debug.LogWarning("Build is disabled");
            }

            return report;
        }

        public static bool FilePathHasInvalidChars(string path)
        {
            return (!string.IsNullOrEmpty(path) && path.IndexOfAny(Path.GetInvalidPathChars()) >= 0);
        }

        public string buildFullPath
        {
            get
            {
                var args = new BuildPathArgs();
                args.ProjectName = Application.productName;
                args.BuildTemplateName = name;
                args.DateTimeNow = DateTime.Now;
                args.Counter = 1;
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

        public bool foundBuildExecutable
        {
            get
            {
                string buildPath;
                try
                {
                    buildPath = buildFullPath;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return false;
                }
                return File.Exists(Path.Combine(buildPath + ExecutableName));
            }
        }

        public bool canRunFromEditor
        {
            get
            {
                if (Profile == null) return false;
#if UNITY_EDITOR_WIN
                return Profile.Target == BuildTarget.StandaloneWindows64 || Profile.Target == BuildTarget.StandaloneWindows;
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
            ProcessStartInfo info = new ProcessStartInfo();
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
                return;
#endif

            Process process = Process.Start(info);
        }

        public void RunBuild()
        {
            bool canRun = Profile != null && !OpenInExplorer && canRunFromEditor;

            string path = buildFullPath;

            if (canRun)
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = path + ExecutableName;
                info.Arguments = RunWithArguments;
                info.WorkingDirectory = path;
                info.UseShellExecute = false;

                EditorUtility.DisplayProgressBar("Build Frontend", $"Running Player : {info.FileName}", 1.0f);
                Process process = Process.Start(info);
                EditorUtility.ClearProgressBar();
            }
            else
            {
                OpenBuild(path);
            }
        }
    }

}