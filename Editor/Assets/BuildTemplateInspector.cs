using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BuildFrontend
{
    [CustomEditor(typeof(BuildTemplate), true)]
    public class BuildTemplateInspector : Editor
    {
        // =================================================================================

        SerializedProperty _nameProperty;
        SerializedProperty _categoryProperty;

        SerializedProperty _profileProperty;
        SerializedProperty _sceneListProperty;
        SerializedProperty _scriptingDefineListProperty;
        SerializedProperty _enabledScriptDefinesProperty;

        SerializedProperty _buildPathProperty;
        SerializedProperty _executableNameProperty;

        SerializedProperty _cleanupBeforeBuildProperty;
        SerializedProperty _openInExplorerProperty;
        SerializedProperty _runWithArgsProperty;

        SerializedProperty _processorsProperty;

        // ------------------------------------------------------

        SerializedObject _profileObject;

        SerializedProperty _buildTargetProperty;

        SerializedProperty _detailedBuildReportProperty;

        SerializedProperty _compressionMethodProperty;
        SerializedProperty _developmentBuildProperty;
        SerializedProperty _copyPdbFilesProperty;
        SerializedProperty _autoConnectProfilerProperty;
        SerializedProperty _deepProfilingProperty;
        SerializedProperty _allowScriptDebuggingProperty;
        SerializedProperty _shaderLiveLinkProperty;

        // ------------------------------------------------------

        SerializedObject _sceneListObject;

        SerializedProperty _scenesProperty;

        // ------------------------------------------------------

        UnityEngine.Object _lastKnownScriptingDefineListObject;

        // ------------------------------------------------------

        Texture2D _infoIcon;
        Texture2D _errorIcon;

        GUISkin _skin;

        readonly GUIContent _label = new();

        ReorderableList _scenesReorderableList;
        ReorderableList _scriptingDefineReorderableList;

        // ------------------------------------------------------

        const int INDENT = 15;

        // =================================================================================

        void OnEnable()
        {
            _nameProperty = serializedObject.FindProperty(nameof(BuildTemplate.Name));
            _categoryProperty = serializedObject.FindProperty(nameof(BuildTemplate.Category));
            _profileProperty = serializedObject.FindProperty(nameof(BuildTemplate.Profile));
            _sceneListProperty = serializedObject.FindProperty(nameof(BuildTemplate.SceneList));
            _scriptingDefineListProperty = serializedObject.FindProperty(nameof(BuildTemplate.ScriptingDefineList));
            _enabledScriptDefinesProperty = serializedObject.FindProperty(nameof(BuildTemplate.EnabledScriptDefines));

            _buildPathProperty = serializedObject.FindProperty(nameof(BuildTemplate.BuildPath));
            _executableNameProperty = serializedObject.FindProperty(nameof(BuildTemplate.ExecutableName));

            _cleanupBeforeBuildProperty = serializedObject.FindProperty(nameof(BuildTemplate.CleanupBeforeBuild));
            _openInExplorerProperty = serializedObject.FindProperty(nameof(BuildTemplate.OpenInExplorer));
            _runWithArgsProperty = serializedObject.FindProperty(nameof(BuildTemplate.RunWithArguments));

            _processorsProperty = serializedObject.FindProperty(nameof(BuildTemplate.Processors));

            _infoIcon = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)
                .FindStyle("CN EntryInfoIcon")?.normal.background;
            _errorIcon = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)
                .FindStyle("CN EntryErrorIcon")?.normal.background;

            // ------------------------------------------------------

            _scenesReorderableList = new ReorderableList(_sceneListObject, null,
                true, true, true, true);

            _lastKnownScriptingDefineListObject = _scriptingDefineListProperty.objectReferenceValue;
            RefreshProfileObject();
            RefreshSceneListObject();

            // ------------------------------------------------------

            _scriptingDefineReorderableList = new ReorderableList(serializedObject, _enabledScriptDefinesProperty,
                true, true, true, true);

            _scriptingDefineReorderableList.elementHeight = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).textField.lineHeight + 6;
            _scriptingDefineReorderableList.drawHeaderCallback = DrawScriptingDefineHeader;
            _scriptingDefineReorderableList.drawElementCallback = DrawScriptDefineElement;

            _scriptingDefineReorderableList.onAddCallback = OnAddScriptDefine;
            _scriptingDefineReorderableList.onRemoveCallback = OnRemoveScriptDefine;
            _scriptingDefineReorderableList.onReorderCallbackWithDetails = OnReorderScriptDefine;

            serializedObject.Update();
            RefreshScriptDefineList();
            serializedObject.ApplyModifiedProperties();

            // ------------------------------------------------------
        }

        void RefreshScriptDefineList()
        {
            _lastKnownScriptingDefineListObject = _scriptingDefineListProperty.objectReferenceValue;

            if (_scriptingDefineListProperty.objectReferenceValue == null)
            {
                return;
            }

            var scriptingDefineListObject = new SerializedObject(_scriptingDefineListProperty.objectReferenceValue);
            var scriptingDefinesProperty = scriptingDefineListObject.FindProperty(nameof(ScriptingDefineList.ScriptingDefines));

            while (_enabledScriptDefinesProperty.arraySize < scriptingDefinesProperty.arraySize)
            {
                _enabledScriptDefinesProperty.InsertArrayElementAtIndex(0);
            }

            for (int n = 0, len = scriptingDefinesProperty.arraySize; n < len; ++n)
            {
                var elementOther = scriptingDefinesProperty.GetArrayElementAtIndex(n);

                var elementMine = _enabledScriptDefinesProperty.GetArrayElementAtIndex(n);

                var elementMineName = elementMine.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.DefineName));

                if (elementMineName.stringValue != elementOther.stringValue)
                {
                    elementMineName.stringValue = elementOther.stringValue;
                    var elementEnabled = elementMine.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.Enable));
                    elementEnabled.boolValue = true;
                }
            }
        }

        void RefreshProfileObject()
        {
            if (_profileProperty.objectReferenceValue == null)
            {
                _profileObject = null;
                return;
            }

            _profileObject = new SerializedObject(_profileProperty.objectReferenceValue);

            _buildTargetProperty = _profileObject.FindProperty(nameof(BuildProfile.Target));
            _detailedBuildReportProperty = _profileObject.FindProperty(nameof(BuildProfile.DetailedBuildReport));
            _compressionMethodProperty = _profileObject.FindProperty(nameof(BuildProfile.CompressionMethod));
            _developmentBuildProperty = _profileObject.FindProperty(nameof(BuildProfile.DevelopmentBuild));
            _copyPdbFilesProperty = _profileObject.FindProperty(nameof(BuildProfile.CopyPDBFiles));
            _autoConnectProfilerProperty = _profileObject.FindProperty(nameof(BuildProfile.AutoConnectProfiler));
            _deepProfilingProperty = _profileObject.FindProperty(nameof(BuildProfile.DeepProfiling));
            _allowScriptDebuggingProperty = _profileObject.FindProperty(nameof(BuildProfile.AllowScriptDebugging));
            _shaderLiveLinkProperty = _profileObject.FindProperty(nameof(BuildProfile.ShaderLiveLink));
        }

        void RefreshSceneListObject()
        {
            if (_sceneListProperty.objectReferenceValue == null)
            {
                _sceneListObject = null;
                _scenesProperty = null;
                return;
            }

            _sceneListObject = new SerializedObject(_sceneListProperty.objectReferenceValue);
            _scenesProperty = _sceneListObject.FindProperty(nameof(SceneList.Scenes));

            _scenesReorderableList.serializedProperty = _scenesProperty;

            _scenesReorderableList.elementHeight =
                EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).textField.lineHeight + 6;
            _scenesReorderableList.drawHeaderCallback = DrawSceneListHeader;
            _scenesReorderableList.drawElementCallback = DrawSceneElement;

            _scenesReorderableList.onAddCallback = OnAddScene;
            _scenesReorderableList.onRemoveCallback = OnRemoveScene;
            _scenesReorderableList.onReorderCallbackWithDetails = OnReorderScene;
        }

        // =================================================================================

        static readonly ReorderableList.HeaderCallbackDelegate DrawSceneListHeader = _DrawSceneListHeader;
        static void _DrawSceneListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Scenes");
        }

        // ------------------------------------------------------

        ReorderableList.AddCallbackDelegate OnAddScene => _OnAddScene;
        void _OnAddScene(ReorderableList list)
        {
            int size = _scenesProperty.arraySize;
            int idx = list.index;
            if (idx < 0)
            {
                // nothing selected. insert to end of list.
                idx = size;
            }

            _scenesProperty.InsertArrayElementAtIndex(idx);
        }

        // ------------------------------------------------------

        ReorderableList.RemoveCallbackDelegate OnRemoveScene => _OnRemoveScene;
        void _OnRemoveScene(ReorderableList list)
        {
            int idx = list.index;
            _scenesProperty.DeleteArrayElementAtIndex(idx);
        }

        // ------------------------------------------------------

        ReorderableList.ReorderCallbackDelegateWithDetails OnReorderScene => _OnReorderScene;
        void _OnReorderScene(ReorderableList list, int oldIdx, int newIdx)
        {
        }

        // ------------------------------------------------------

        ReorderableList.ElementCallbackDelegate DrawSceneElement => _DrawSceneElement;
        void _DrawSceneElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _scenesReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            CalcSize.text = "99";
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(CalcSize).x + 5;

            _label.image = null;
            _label.text = index.ToString();

            EditorGUI.PropertyField(new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4), element, _label);
        }

        // =================================================================================

        static readonly ReorderableList.HeaderCallbackDelegate DrawScriptingDefineHeader = _DrawScriptingDefineHeader;
        static void _DrawScriptingDefineHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Scripting Defines");
        }

        // ------------------------------------------------------

        ReorderableList.AddCallbackDelegate OnAddScriptDefine => _OnAddScriptDefine;
        void _OnAddScriptDefine(ReorderableList list)
        {
            int size = _enabledScriptDefinesProperty.arraySize;
            int idx = list.index;
            if (idx < 0)
            {
                // nothing selected. insert to end of list.
                idx = size;
            }

            bool firstAdding = size == 0;

            _enabledScriptDefinesProperty.InsertArrayElementAtIndex(idx);
            if (firstAdding)
            {
                var element = _enabledScriptDefinesProperty.GetArrayElementAtIndex(0);
                var enableProperty = element.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.Enable));
                enableProperty.boolValue = true;
            }

            // add it to ScriptingDefineList as well
            if (_scriptingDefineListProperty.objectReferenceValue != null)
            {
                var scriptingDefineListObject = new SerializedObject(_scriptingDefineListProperty.objectReferenceValue);
                var scriptingDefinesProperty = scriptingDefineListObject.FindProperty(nameof(ScriptingDefineList.ScriptingDefines));

                scriptingDefineListObject.Update();

                scriptingDefinesProperty.InsertArrayElementAtIndex(idx);

                scriptingDefineListObject.ApplyModifiedProperties();
            }
        }

        // ------------------------------------------------------

        ReorderableList.RemoveCallbackDelegate OnRemoveScriptDefine => _OnRemoveScriptDefine;
        void _OnRemoveScriptDefine(ReorderableList list)
        {
            int idx = list.index;
            _enabledScriptDefinesProperty.DeleteArrayElementAtIndex(idx);

            // remove it from ScriptingDefineList as well
            if (_scriptingDefineListProperty.objectReferenceValue != null)
            {
                var scriptingDefineListObject = new SerializedObject(_scriptingDefineListProperty.objectReferenceValue);
                var scriptingDefinesProperty = scriptingDefineListObject.FindProperty(nameof(ScriptingDefineList.ScriptingDefines));

                scriptingDefineListObject.Update();

                scriptingDefinesProperty.DeleteArrayElementAtIndex(idx);

                scriptingDefineListObject.ApplyModifiedProperties();
            }
        }

        // ------------------------------------------------------

        ReorderableList.ReorderCallbackDelegateWithDetails OnReorderScriptDefine => _OnReorderScriptDefine;
        void _OnReorderScriptDefine(ReorderableList list, int oldIdx, int newIdx)
        {
            // reorder it in the ScriptingDefineList as well
            if (_scriptingDefineListProperty.objectReferenceValue != null)
            {
                var scriptingDefineListObject = new SerializedObject(_scriptingDefineListProperty.objectReferenceValue);
                var scriptingDefinesProperty = scriptingDefineListObject.FindProperty(nameof(ScriptingDefineList.ScriptingDefines));

                scriptingDefineListObject.Update();

                scriptingDefinesProperty.MoveArrayElement(oldIdx, newIdx);

                scriptingDefineListObject.ApplyModifiedProperties();
            }
        }

        // ------------------------------------------------------

        ReorderableList.ElementCallbackDelegate DrawScriptDefineElement => _DrawScriptDefineElement;
        void _DrawScriptDefineElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _scriptingDefineReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            var enabledProperty = element.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.Enable));
            var nameProperty = element.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.DefineName));

            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), enabledProperty, GUIContent.none);
            var prevName = nameProperty.stringValue;
            EditorGUI.PropertyField(new Rect(rect.x+20, rect.y + 2, rect.width - 20, rect.height - 4), nameProperty, GUIContent.none);
            if (prevName != nameProperty.stringValue)
            {
                if (_scriptingDefineListProperty.objectReferenceValue != null)
                {
                    var scriptingDefineListObject = new SerializedObject(_scriptingDefineListProperty.objectReferenceValue);
                    var scriptingDefinesProperty = scriptingDefineListObject.FindProperty(nameof(ScriptingDefineList.ScriptingDefines));

                    scriptingDefineListObject.Update();

                    var elementOther = scriptingDefinesProperty.GetArrayElementAtIndex(index);
                    elementOther.stringValue = nameProperty.stringValue;

                    scriptingDefineListObject.ApplyModifiedProperties();
                }
            }
        }

        // =================================================================================

        public override void OnInspectorGUI()
        {
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

            serializedObject.Update();

            DrawProperty(_nameProperty);
            DrawProperty(_categoryProperty);

            // -------------------------------------------

            DrawProperty(_profileProperty);
            if (_profileProperty.objectReferenceValue == null && _profileObject != null ||
                _profileProperty.objectReferenceValue != null && _profileObject == null ||
                _profileProperty.objectReferenceValue != null && _profileObject != null &&
                _profileProperty.objectReferenceValue != _profileObject.targetObject)
            {
                RefreshProfileObject();
            }
            DrawProfileProperties();

            // -------------------------------------------

            DrawProperty(_sceneListProperty);
            if (_sceneListProperty.objectReferenceValue == null && _sceneListObject != null ||
                _sceneListProperty.objectReferenceValue != null && _sceneListObject == null ||
                _sceneListProperty.objectReferenceValue != null && _sceneListObject != null &&
                _sceneListProperty.objectReferenceValue != _sceneListObject.targetObject)
            {
                RefreshSceneListObject();
            }
            DrawSceneList();

            // -------------------------------------------

            DrawProperty(_scriptingDefineListProperty);
            if (_scriptingDefineListProperty.objectReferenceValue == null && _lastKnownScriptingDefineListObject != null ||
                _scriptingDefineListProperty.objectReferenceValue != null && _lastKnownScriptingDefineListObject == null ||
                _scriptingDefineListProperty.objectReferenceValue != null && _lastKnownScriptingDefineListObject != null &&
                _scriptingDefineListProperty.objectReferenceValue != _lastKnownScriptingDefineListObject)
            {
                RefreshScriptDefineList();
            }
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(INDENT);
                _scriptingDefineReorderableList.DoLayoutList();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // -------------------------------------------

            DrawProperty(_buildPathProperty);
            DrawOutputPath(buildTemplate);

            DrawProperty(_executableNameProperty);

            DrawProperty(_cleanupBeforeBuildProperty);
            DrawProperty(_openInExplorerProperty);

            DrawProperty(_runWithArgsProperty);

            GUILayout.Space(20);
            DrawProperty(_processorsProperty);

            serializedObject.ApplyModifiedProperties();
        }

        static readonly GUIContent CalcSize = new();
        static void DrawProperty(SerializedProperty property, GUILayoutOption[] options = null)
        {
            CalcSize.text = property.name;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(CalcSize).x + 10;
            EditorGUILayout.PropertyField(property, options);
        }
        static void DrawProperty(string customLabel, SerializedProperty property, GUILayoutOption[] options = null)
        {
            CalcSize.text = customLabel;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(CalcSize).x + 10;
            EditorGUILayout.PropertyField(property, options);
        }

        void DrawProfileProperties()
        {
            if (_profileProperty.objectReferenceValue == null)
            {
                // BuildProfile not assigned
                return;
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(INDENT);
                GUILayout.BeginVertical("box");
                {
                    DrawProperty(_buildTargetProperty);
                    GUILayout.Space(20);
                    DrawProperty(_detailedBuildReportProperty);
                    DrawProperty(_compressionMethodProperty);

                    GUILayout.Space(20);
                    DrawProperty(_developmentBuildProperty);

                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = prevEnabled && _developmentBuildProperty.boolValue;

                    DrawProperty(_copyPdbFilesProperty);
                    DrawProperty(_autoConnectProfilerProperty);
                    DrawProperty(_deepProfilingProperty);
                    DrawProperty(_allowScriptDebuggingProperty);
                    DrawProperty(_shaderLiveLinkProperty);

                    GUI.enabled = prevEnabled;
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(25);
        }

        void DrawSceneList()
        {
            if (_sceneListProperty.objectReferenceValue == null || _sceneListObject == null)
            {
                // BuildProfile not assigned
                return;
            }

            _sceneListObject.Update();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(INDENT);
                _scenesReorderableList.DoLayoutList();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            _sceneListObject.ApplyModifiedProperties();
        }

        void DrawOutputPath(BuildTemplate buildTemplate)
        {
            string outputPath;
            bool success;
            try
            {
                outputPath = buildTemplate.BuildFullPath;
                success = true;

                _label.image = _infoIcon;
                _label.text = "Output Path:";
            }
            catch (Exception e)
            {
                outputPath = e.Message;
                success = false;

                _label.image = _errorIcon;
                _label.text = "Error in Output Path:";
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(INDENT);
                GUILayout.BeginVertical("box");
                {
                    GUILayout.Label(_label);

                    GUILayout.BeginHorizontal();
                    {
                        if (!success)
                        {
                            GUI.skin = _skin;
                        }
                        GUILayout.Label(outputPath);

                        if (success && GUILayout.Button("Open", BuildFrontend.Styles.NoExpandWidth))
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
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
    }
}