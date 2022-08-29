using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildFrontend
{
    [CustomEditor(typeof(BuildTemplate), true)]
    public class BuildTemplateInspector : Editor
    {
        GUISkin _skin;

        readonly GUIContent _label = new();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var buildTemplate = target as BuildTemplate;
            if (buildTemplate == null)
            {
                return;
            }

            _skin = AssetDatabase.LoadAssetAtPath<GUISkin>("Packages/com.anomalousunderdog.build-frontend/Editor/GUISkin.guiskin");
            if (_skin == null)
            {
                Debug.LogError("Could not load guiskin");
                return;
            }

            GUILayout.Space(20);

            string outputPath;
            try
            {
                outputPath = buildTemplate.buildFullPath;
            }
            catch (Exception e)
            {
                _label.image = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)
                    .FindStyle("CN EntryErrorIcon").normal.background;
                _label.text = "Error in Output Path:";

                GUILayout.Label(_label);
                GUI.skin = _skin;
                GUILayout.Label(e.Message);
                return;
            }

            _label.image = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)
                .FindStyle("CN EntryInfoIcon").normal.background;
            _label.text = "Output Path:";

            GUILayout.Label(_label);
            GUILayout.Label(outputPath);

            if (GUILayout.Button("Open"))
            {
                // keep going up the parent folder until we find one that exists
                while (!string.IsNullOrEmpty(outputPath) && !Directory.Exists(outputPath))
                {
                    DirectoryInfo parentDir = Directory.GetParent(outputPath);
                    outputPath = parentDir?.FullName;
                }

                BuildTemplate.OpenBuild(outputPath);
            }
        }
    }
}