using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BuildFrontend
{
    public class BuildFrontend : EditorWindow
    {
        public const int CreateAssetMenuPriority = 801;
        public const int WindowMenuPriority = 203;

        [MenuItem("File/Build Frontend %&B", priority = WindowMenuPriority)]

        static void OpenWindow()
        {
            var window = GetWindow<BuildFrontend>();
            window.PopulateAssets();
        }

        private void OnEnable()
        {
            titleContent = Contents.windowTitle;
            m_CurrentLog = "Ready to build!";
            PopulateAssets();
        }

        string m_CurrentLog = "Ready to build!";

        readonly GUIContent m_Content = new();

        readonly Dictionary<BuildTemplate, BuildReport> m_Reports = new();

        readonly Queue<Action> m_Actions = new();

        BuildTemplate m_CurrentBuild;

        void EnqueueAction(Action action)
        {
            m_Actions.Enqueue(action);
        }

        void OnInspectorUpdate()
        {
            var nextAction = m_Actions.Count == 0 ? null : m_Actions.Dequeue();
            if (nextAction == null)
            {
                return;
            }

            var selected = Selection.activeObject;
            try
            {
                nextAction.Invoke();
            }
            finally
            {
                Selection.activeObject = selected;
            }
        }

        void OnGUI()
        {
            GUILayout.BeginHorizontal(Styles.HeightIconSize);
            {
                var rect = GUILayoutUtility.GetRect(Styles.iconSize, Styles.iconSize, Styles.Icon, Styles.WidthIconSize);
                GUI.DrawTexture(rect, Contents.icon);
                GUILayout.BeginVertical();
                {
                    GUILayout.Space(8);
                    GUILayout.Label(Contents.title, Styles.Title);
                    GUILayout.Label(m_CurrentLog);
                    GUILayout.Space(8);
                }
                GUILayout.EndVertical();
                GUILayout.Space(8);

                GUILayout.BeginVertical(Styles.Width120);
                {
                    GUILayout.Space(8);
                    if (GUILayout.Button("Build All", Styles.BuildButton, Styles.Height32))
                    {
                        DoAllBuild();
                    }
                    GUILayout.Space(8);
                }
                GUILayout.EndVertical();
                GUILayout.Space(8);
            }
            GUILayout.EndHorizontal();

            var r = GUILayoutUtility.GetRect(-1, 1, Styles.ExpandWidth);
            EditorGUI.DrawRect(r, Color.black);

            GUILayout.BeginHorizontal();
            {
                DrawTemplateList();
                DrawReport();
            }
            GUILayout.EndHorizontal();
        }

        void DrawReport()
        {
            if (selectedTemplate == null)
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal(Styles.Height32);
                    {
                        GUILayout.Space(180);
                        EditorGUILayout.HelpBox("Please select a build template in the left pane list", MessageType.Info);
                        GUILayout.Space(180);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndVertical();
                return;
            }

            BuildReport report = null;
            if (m_Reports.ContainsKey(selectedTemplate))
                report = m_Reports[selectedTemplate];

            reportScroll = EditorGUILayout.BeginScrollView(reportScroll, Styles.scrollView);

            FormatHeaderGUI(selectedTemplate, report);

            if (report != null)
                FormatReportGUI(selectedTemplate, report);
            else
                EditorGUILayout.HelpBox("No Build report has been generated yet, please build this template first", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        void DoAllBuild()
        {
            foreach (var cat in m_BuildTemplates)
            {
                foreach (var template in cat.Value)
                {
                    EditorUtility.DisplayProgressBar("Build Frontend", $"Building {template.name} ...", 1.0f);
                    if (template.BuildEnabled)
                    {
                        EnqueueBuildTemplate(template, false);
                    }
                }
            }

            // Finally...
            EnqueueAction(() =>
            {
                m_CurrentLog = $"[{DateTime.Now.ToShortTimeString()}] Finished Building All Templates";
                Repaint();
            });

        }

        Vector2 templateScroll = Vector2.zero;
        Vector2 reportScroll = Vector2.zero;

        void DrawTemplateList()
        {
            templateScroll = GUILayout.BeginScrollView(templateScroll, false, true, Styles.Width240);

            GUILayout.BeginVertical(EditorStyles.label);
            {
                foreach (var catKVP in m_BuildTemplates)
                {
                    EditorGUILayout.LabelField(catKVP.Key == string.Empty ? "General" : catKVP.Key, EditorStyles.boldLabel);

                    foreach (var template in catKVP.Value)
                    {
                        GUILayoutUtility.GetRect(-1, 24, Styles.ExpandWidth);

                        // Draw background box

                        Rect r = GUILayoutUtility.GetLastRect();
                        Vector2 pos = r.position;

                        r.position = pos;
                        r.height = 22;

                        Rect buttonRect = r;
                        buttonRect.xMin += 24;
                        buttonRect.height = 24;

                        if (GUI.Button(buttonRect, GUIContent.none, Styles.hoverBG))
                        {
                            selectedTemplate = template;
                            Selection.activeObject = selectedTemplate;
                        }

                        if (template == selectedTemplate)
                        {
                            float gray = EditorGUIUtility.isProSkin ? 1 : 0;
                            EditorGUI.DrawRect(r, new Color(gray, gray, gray, 0.1f));
                        }


                        Rect toggleRect = r;
                        toggleRect.xMin += 4;
                        toggleRect.width = 24;

                        EditorGUI.BeginChangeCheck();
                        var enabled = GUI.Toggle(toggleRect, template.BuildEnabled, GUIContent.none);
                        if (EditorGUI.EndChangeCheck())
                        {
                            template.BuildEnabled = enabled;
                            EditorUtility.SetDirty(template);
                        }

                        Rect iconRect = r;
                        iconRect.xMin += 24;
                        iconRect.yMin += 2;
                        iconRect.width = 18;
                        iconRect.height = 18;
                        Texture icon = Contents.iconGray;
                        if (m_CurrentBuild != null && m_CurrentBuild == template)
                        {
                            icon = Contents.iconBlue;
                        }
                        else if (m_Reports != null && m_Reports.ContainsKey(template))
                        {
                            var summary = m_Reports[template].summary;
                            var result = summary.result;
                            switch (result)
                            {
                                default:
                                case BuildResult.Unknown:
                                    icon = Contents.iconGray;
                                    break;
                                case BuildResult.Succeeded:
                                    if (summary.totalWarnings == 0)
                                        icon = Contents.iconGreen;
                                    else
                                        icon = Contents.iconOrange;
                                    break;
                                case BuildResult.Cancelled:
                                case BuildResult.Failed:
                                    icon = Contents.iconRed;
                                    break;
                            }
                        }

                        GUI.DrawTexture(iconRect, icon);


                        Rect labelRect = r;
                        labelRect.xMin += 48;

                        GUI.Label(labelRect, template.Name != null && template.Name != string.Empty ? template.Name : template.name, template == selectedTemplate ? EditorStyles.boldLabel : EditorStyles.label);

                    }
                }
            }
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        [SerializeField]
        BuildTemplate selectedTemplate;

        Dictionary<string, List<BuildTemplate>> m_BuildTemplates;
        List<BuildProfile> m_BuildProfiles;
        List<SceneList> m_SceneLists;

        void PopulateAssets()
        {
            var buildTemplates = AssetDatabase.FindAssets("t:BuildTemplate");
            var buildProfiles = AssetDatabase.FindAssets("t:BuildProfile");
            var sceneLists = AssetDatabase.FindAssets("t:SceneList");

            m_BuildTemplates = new Dictionary<string, List<BuildTemplate>>();
            m_BuildProfiles = new List<BuildProfile>();
            m_SceneLists = new List<SceneList>();


            foreach (var templateGUID in buildTemplates)
            {
                string templatePath = AssetDatabase.GUIDToAssetPath(templateGUID);
                BuildTemplate template = (BuildTemplate)AssetDatabase.LoadAssetAtPath(templatePath, typeof(BuildTemplate));
                if (!m_BuildTemplates.ContainsKey(template.Category))
                    m_BuildTemplates.Add(template.Category, new List<BuildTemplate>());

                m_BuildTemplates[template.Category].Add(template);
            }


            foreach (var profileGUID in buildProfiles)
            {
                string profilePath = AssetDatabase.GUIDToAssetPath(profileGUID);
                BuildProfile profile = (BuildProfile)AssetDatabase.LoadAssetAtPath(profilePath, typeof(BuildProfile));
                m_BuildProfiles.Add(profile);
            }

            foreach (var sceneListGUID in sceneLists)
            {
                string sceneListPath = AssetDatabase.GUIDToAssetPath(sceneListGUID);
                SceneList sceneList = (SceneList)AssetDatabase.LoadAssetAtPath(sceneListPath, typeof(SceneList));
                m_SceneLists.Add(sceneList);
            }
        }

        static string FormatSize(ulong byteSize)
        {
            double size = byteSize;
            if (size < 1024) // Bytes
            {
                return $"{size.ToString("F2")} bytes";
            }
            else if (size < 1024 * 1024) // KiloBytes
            {
                return $"{(size / 1024).ToString("F2")} KiB ({byteSize} bytes)";
            }
            else if (size < 1024 * 1024 * 1024) // Megabytes
            {
                return $"{(size / (1024 * 1024)).ToString("F2")} MiB ({byteSize} bytes)";
            }
            else // Gigabytes
            {
                return $"{(size / (1024 * 1024 * 1024)).ToString("F2")} GiB ({byteSize} bytes)";
            }
        }

        void FormatHeaderGUI(BuildTemplate template, BuildReport report = null)
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.Label($"{(template.Name == string.Empty ? template.name : template.Name)} {(template.Category == string.Empty ? "" : $"({template.Category})")}", Styles.boldLabelLarge);

                    GUILayout.BeginHorizontal();
                    {
                        if (report != null)
                        {
                            var summary = report.summary;
                            if (summary.result == BuildResult.Succeeded)
                                GUILayout.Label(Contents.buildSucceeded, Styles.Width32);
                            else if (summary.result != BuildResult.Unknown)
                                GUILayout.Label(Contents.buildFailed, Styles.Width32);

                            GUILayout.Label(summary.result.ToString(), Styles.boldLabelLarge);
                        }
                        else
                        {
                            GUILayout.Label(Contents.buildPending, Styles.Width32);
                            GUILayout.Label("Build not yet started", Styles.boldLabelLarge);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical(Styles.Width120);
                {
                    GUILayout.BeginHorizontal();
                    {
                        EditorGUI.BeginDisabledGroup(template == null);

                        if (GUILayout.Button("Build", Styles.MiniButtonLeft))
                        {
                            EnqueueBuildTemplate(template, false);
                        }
                        if (GUILayout.Button("+ Run", Styles.MiniButtonRight, Styles.Width48))
                        {
                            EnqueueBuildTemplate(template, true);
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    GUILayout.EndHorizontal();

                    if (template.CanRunFromEditor && !template.OpenInExplorer)
                    {
                        EditorGUI.BeginDisabledGroup(template == null || !template.BuildExecutableExists);

                        if (GUILayout.Button("Run Last Build", Styles.MiniButton))
                        {
                            EnqueueAction(() =>
                            {
                                template.RunBuild();
                                m_CurrentLog = $"[{DateTime.Now.ToShortTimeString()}] Started running template: {template.name} ...";
                                Repaint();
                            });
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        if (GUILayout.Button("Open Output Folder", Styles.MiniButton))
                        {
                            EnqueueAction(() =>
                            {
                                template.RunBuild();
                                Repaint();
                            });
                        }
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            var r = GUILayoutUtility.GetRect(-1, 1, Styles.ExpandWidth);
            EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.5f));
            GUILayout.Space(16);
        }

        void EnqueueBuildTemplate(BuildTemplate template, bool runAfterBuild = false)
        {
            EnqueueAction(() =>
            {
                m_CurrentLog = $"[{DateTime.Now.ToShortTimeString()}] Started Building Template : {template.name} ...";
                m_CurrentBuild = template;
            });

            EnqueueAction(() => { Repaint(); });

            EnqueueAction(() =>
            {
                var report = template.DoBuild(runAfterBuild);
                if (report != null)
                    m_Reports[template] = report;
            });

            EnqueueAction(() =>
            {
                m_CurrentLog = $"[{DateTime.Now.ToShortTimeString()}] Finished Building Template : {template.name} ...";
                m_CurrentBuild = null;
                selectedTemplate = template;
            });

            EnqueueAction(() => { Repaint(); });
        }

        void FormatReportGUI(BuildTemplate template, BuildReport report)
        {
            var summary = report.summary;

            if (GUILayout.Button("Open Report Details..."))
            {
                Selection.activeObject = report;
            }

            GUILayout.Space(8);
            GUILayout.Label($"Total Build Time : {summary.totalTime}");
            EditorGUILayout.LabelField($"Build Size : {FormatSize(summary.totalSize)} ");

            if (summary.totalErrors > 0)
            {
                m_Content.image = Contents.errorIconSmall.image;
                m_Content.text = $"{summary.totalErrors} Errors";
                GUILayout.Label(m_Content);
            }

            if (summary.totalWarnings > 0)
            {
                m_Content.image = Contents.warnIconSmall.image;
                m_Content.text = $"{summary.totalWarnings} Warnings";
                GUILayout.Label(m_Content);
            }

            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.TextField("Output Path", report.summary.outputPath);

                if (GUILayout.Button("Open Folder", Styles.NoExpandWidth))
                {
                    DirectoryInfo parentDir = Directory.GetParent(report.summary.outputPath);
                    string parentPath = parentDir?.FullName;
                    BuildTemplate.OpenBuild(parentPath);
                }

                if (GUILayout.Button("Run", Styles.NoExpandWidth))
                {
                    BuildTemplate.RunBuild(report.summary.outputPath);
                }
            }
            GUILayout.EndHorizontal();

            if (report.strippingInfo != null)
            {
                GUILayout.Space(8);

                GUILayout.Label("Included Modules", EditorStyles.boldLabel);
                var modules = report.strippingInfo.includedModules;
                foreach (var module in modules)
                {
                    EditorGUILayout.LabelField(module, EditorStyles.foldout);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("Build Steps", EditorStyles.boldLabel);
            var steps = report.steps;
            foreach (var step in steps)
            {
                string prefName = $"BuildFrontend.Foldout'{step.name}'";

                GUILayout.BeginHorizontal();
                {
                    if (step.messages.Any(o => o.type == LogType.Error || o.type == LogType.Assert || o.type == LogType.Exception))
                        GUILayout.Label(Contents.errorIconSmall, Styles.Icon, Styles.Width16);
                    else if (step.messages.Any(o => o.type == LogType.Warning))
                        GUILayout.Label(Contents.warnIconSmall, Styles.Icon, Styles.Width16);
                    else
                        GUILayout.Label(Contents.successIcon, Styles.Icon, Styles.Width16);

                    if (step.messages.Length > 0)
                    {
                        bool pref = EditorPrefs.GetBool(prefName, false);

                        bool newPref = EditorGUILayout.Foldout(pref, step.name);

                        if (GUI.changed && pref != newPref)
                            EditorPrefs.SetBool(prefName, newPref);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(step.name);
                    }
                }
                GUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                if (EditorPrefs.GetBool(prefName, false))
                {
                    foreach (var message in step.messages)
                    {
                        MessageType type = MessageType.Error;
                        if (message.type == LogType.Log)
                            type = MessageType.Info;
                        else if (message.type == LogType.Warning)
                            type = MessageType.Warning;

                        EditorGUILayout.HelpBox(message.content, type, true);
                    }
                }
                EditorGUI.indentLevel--;

                GUILayout.Space(2);
            }
            GUILayout.Space(128);
        }

        public static class Styles
        {
            public static GUIStyle BuildButton;
            public static GUIStyle MiniButton;
            public static GUIStyle MiniButtonLeft;
            public static GUIStyle MiniButtonRight;
            public static GUIStyle progressBarItem;
            public static GUIStyle SelectedProfile;
            public static GUIStyle Title;
            public static GUIStyle Icon;
            public static GUIStyle boldLabelLarge;
            public static GUIStyle scrollView;
            public static GUIStyle hoverBG;

            public const int iconSize = 48;

            public static readonly GUILayoutOption[] ExpandWidth = {GUILayout.ExpandWidth(true)};
            public static readonly GUILayoutOption[] NoExpandWidth = {GUILayout.ExpandWidth(false)};

            public static readonly GUILayoutOption[] Width16 = {GUILayout.Width(16)};
            public static readonly GUILayoutOption[] Width32 = { GUILayout.Width(32)};
            public static readonly GUILayoutOption[] Width48 = { GUILayout.Width(48)};
            public static readonly GUILayoutOption[] Width120 = {GUILayout.Width(120)};
            public static readonly GUILayoutOption[] Width240 = {GUILayout.Width(240)};

            public static readonly GUILayoutOption[] Height32 = {GUILayout.Height(32)};

            public static readonly GUILayoutOption[] WidthIconSize = {GUILayout.Width(iconSize)};
            public static readonly GUILayoutOption[] HeightIconSize = {GUILayout.Height(iconSize)};


            static Styles()
            {
                BuildButton = new GUIStyle(EditorStyles.miniButton);
                BuildButton.fontSize = 14;
                BuildButton.fontStyle = FontStyle.Bold;
                BuildButton.fixedHeight = 32;

                MiniButton = new GUIStyle(EditorStyles.miniButton);
                MiniButton.fixedHeight = 22;
                MiniButton.fontSize = 12;

                MiniButtonLeft = new GUIStyle(EditorStyles.miniButtonLeft);
                MiniButtonLeft.fixedHeight = 22;
                MiniButtonLeft.fontSize = 12;

                MiniButtonRight = new GUIStyle(EditorStyles.miniButtonRight);
                MiniButtonRight.fixedHeight = 22;
                MiniButtonRight.fontSize = 12;

                SelectedProfile = new GUIStyle(EditorStyles.label);
                var pink = EditorGUIUtility.isProSkin ? new Color(1.0f, 0.2f, 0.5f, 1.0f) : new Color(1.0f, 0.05f, 0.4f, 1.0f);
                SelectedProfile.active.textColor = pink;
                SelectedProfile.focused.textColor = pink;
                SelectedProfile.hover.textColor = pink;
                SelectedProfile.normal.textColor = pink;

                Title = new GUIStyle(EditorStyles.label);
                Title.fontSize = 18;

                Icon = new GUIStyle(EditorStyles.label);

                Icon.padding = new RectOffset();
                Icon.margin = new RectOffset();

                progressBarItem = new GUIStyle(EditorStyles.miniLabel);
                progressBarItem.alignment = TextAnchor.MiddleCenter;
                progressBarItem.margin = new RectOffset(0, 0, 0, 0);
                progressBarItem.padding = new RectOffset(0, 0, 0, 0);
                progressBarItem.wordWrap = true;
                progressBarItem.onActive.background = Texture2D.whiteTexture;
                progressBarItem.onFocused.background = Texture2D.whiteTexture;
                progressBarItem.onHover.background = Texture2D.whiteTexture;
                progressBarItem.onNormal.background = Texture2D.whiteTexture;
                progressBarItem.active.background = Texture2D.whiteTexture;
                progressBarItem.focused.background = Texture2D.whiteTexture;
                progressBarItem.hover.background = Texture2D.whiteTexture;
                progressBarItem.normal.background = Texture2D.whiteTexture;

                boldLabelLarge = new GUIStyle(EditorStyles.boldLabel);
                boldLabelLarge.fontSize = 16;

                scrollView = new GUIStyle();
                scrollView.padding = new RectOffset(8, 8, 8, 8);

                hoverBG = new GUIStyle(EditorStyles.foldoutHeader);
                hoverBG.padding = new RectOffset(0, 0, 0, 0);
                hoverBG.margin = new RectOffset(0, 0, 0, 0);
                hoverBG.fixedHeight = 22;


            }
        }
        static class Contents
        {
            public static GUIContent windowTitle;
            public static GUIContent title;
            public static GUIContent build = new GUIContent("Build");
            public static GUIContent template = new GUIContent("Template:");
            public static GUIContent profile = new GUIContent("Profile:");
            public static GUIContent sceneList = new GUIContent("Scene List:");
            public static Texture icon;

            public static GUIContent buildSucceeded = EditorGUIUtility.IconContent("Collab.BuildSucceeded");
            public static GUIContent buildFailed = EditorGUIUtility.IconContent("Collab.BuildFailed");
            public static GUIContent buildPending = EditorGUIUtility.IconContent("Collab.Build");

            public static GUIContent successIcon = EditorGUIUtility.IconContent("Collab");
            public static GUIContent failIcon = EditorGUIUtility.IconContent("CollabError");

            public static GUIContent errorIcon = EditorGUIUtility.IconContent("console.erroricon");
            public static GUIContent errorIconSmall = EditorGUIUtility.IconContent("console.erroricon.sml");

            public static GUIContent infoIcon = EditorGUIUtility.IconContent("console.infoicon");
            public static GUIContent infoIconSmall = EditorGUIUtility.IconContent("console.infoicon.sml");

            public static GUIContent warnIcon = EditorGUIUtility.IconContent("console.warnicon");
            public static GUIContent warnIconSmall = EditorGUIUtility.IconContent("console.warnicon.sml");


            public static Texture iconGray;
            public static Texture iconBlue;
            public static Texture iconGreen;
            public static Texture iconOrange;
            public static Texture iconRed;

            static Contents()
            {
                icon = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.anomalousunderdog.build-frontend/Editor/Icons/BuildFrontend.png");
                var titleIcon = AssetDatabase.LoadAssetAtPath<Texture>($"Packages/com.anomalousunderdog.build-frontend/Editor/Icons/BuildFrontendTab{(EditorGUIUtility.isProSkin ? "" : "Personal")}.png");
                windowTitle = new GUIContent("Build Frontend", titleIcon);
                title = new GUIContent("Build Frontend");

                iconGray = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.anomalousunderdog.build-frontend/Editor/Icons/Icons-NotRun.png");
                iconBlue = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.anomalousunderdog.build-frontend/Editor/Icons/Icons-Running.png");
                iconGreen = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.anomalousunderdog.build-frontend/Editor/Icons/Icons-Green.png");
                iconOrange = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.anomalousunderdog.build-frontend/Editor/Icons/Icons-Warning.png");
                iconRed = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.anomalousunderdog.build-frontend/Editor/Icons/Icons-Failed.png");
            }
        }
    }
}