using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        Texture _plusIcon;

        GUISkin _skin;
        GUIStyle _smallButton;

        readonly GUIContent _label = new();
        readonly GUIContent _addDefineLabel = new();
        readonly GUIContent _duplicateDefineLabel = new();
        readonly GUIContent _duplicateSceneLabel = new();
        readonly GUIContent _loadScenes = new();

        ReorderableList _scenesReorderableList;
        ReorderableList _scriptingDefineReorderableList;

        // ------------------------------------------------------

        const int INDENT = 15;

        static bool ArrayPropertyContains(SerializedProperty arrayProperty, string valueToCheck)
        {
            for (int n = 0, len = arrayProperty.arraySize; n < len; ++n)
            {
                if (arrayProperty.GetArrayElementAtIndex(n).stringValue == valueToCheck)
                {
                    return true;
                }
            }
            return false;
        }

        static bool ArrayPropertyContains(SerializedProperty arrayProperty, string relativeProperty, string valueToCheck)
        {
            for (int n = 0, len = arrayProperty.arraySize; n < len; ++n)
            {
                if (arrayProperty.GetArrayElementAtIndex(n).FindPropertyRelative(relativeProperty).stringValue == valueToCheck)
                {
                    return true;
                }
            }
            return false;
        }

        static int ArrayPropertyIndexOf(SerializedProperty arrayProperty, string valueToCheck)
        {
            for (int n = 0, len = arrayProperty.arraySize; n < len; ++n)
            {
                if (arrayProperty.GetArrayElementAtIndex(n).stringValue == valueToCheck)
                {
                    return n;
                }
            }
            return -1;
        }

        static int ArrayPropertyIndexOf(SerializedProperty arrayProperty, string relativeProperty, string valueToCheck)
        {
            for (int n = 0, len = arrayProperty.arraySize; n < len; ++n)
            {
                if (arrayProperty.GetArrayElementAtIndex(n).FindPropertyRelative(relativeProperty).stringValue == valueToCheck)
                {
                    return n;
                }
            }
            return -1;
        }

        static int ArrayPropertyIndexOf(SerializedProperty arrayProperty, UnityEngine.Object valueToCheck)
        {
            for (int n = 0, len = arrayProperty.arraySize; n < len; ++n)
            {
                if (arrayProperty.GetArrayElementAtIndex(n).objectReferenceValue == valueToCheck)
                {
                    return n;
                }
            }
            return -1;
        }

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
            _plusIcon = EditorGUIUtility.IconContent("Toolbar Plus").image;

            _addDefineLabel.image = _plusIcon;
            _addDefineLabel.tooltip = "This Define is in the Build Template, but not found in the Scripting Define List. Click to add it.";

            _duplicateDefineLabel.image = BuildFrontend.Contents.warnIconSmall.image;
            _duplicateDefineLabel.tooltip = "Should not have duplicate defines. This duplicate will be ignored.";

            _duplicateSceneLabel.image = BuildFrontend.Contents.warnIconSmall.image;
            _duplicateSceneLabel.tooltip = "Should not have duplicate scenes. This scene will be ignored.";

            _loadScenes.image = EditorGUIUtility.IconContent("UnityEditor.HierarchyWindow").image;
            _loadScenes.text = "Load Scenes";
            _loadScenes.tooltip = "Load all the Scenes into the Hierarchy.";

            // ------------------------------------------------------

            RefreshProfileObject();

            // ------------------------------------------------------

            float lineHeight = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).textField.lineHeight + 6;

            _scenesReorderableList = new ReorderableList(_sceneListObject, null,
                true, true, true, true)
            {
                elementHeight = lineHeight,
                drawHeaderCallback = DrawSceneListHeader,
                drawElementCallback = DrawSceneElement,
                drawElementBackgroundCallback = DrawSceneElementBg,
                drawFooterCallback = DrawSceneFooter,
                onAddCallback = OnAddScene,
                //onAddDropdownCallback = OnAddSceneDropdown,
                onRemoveCallback = OnRemoveScene,
                onReorderCallbackWithDetails = OnReorderScene
            };

            _lastKnownScriptingDefineListObject = _scriptingDefineListProperty.objectReferenceValue;
            RefreshSceneListObject();

            // ------------------------------------------------------

            _scriptingDefineReorderableList = new ReorderableList(serializedObject, _enabledScriptDefinesProperty,
                true, true, true, true)
            {
                elementHeight = lineHeight,
                drawHeaderCallback = DrawScriptingDefineHeader,
                drawElementCallback = DrawScriptDefineElement,
                drawElementBackgroundCallback = DrawScriptDefineElementBg,
                onAddCallback = OnAddScriptDefine,
                onRemoveCallback = OnRemoveScriptDefine,
                onReorderCallbackWithDetails = OnReorderScriptDefine
            };

            serializedObject.Update();
            RefreshScriptDefineList();
            serializedObject.ApplyModifiedProperties();

            EditorApplication.update += OnUpdate;

            // ------------------------------------------------------
        }

        void OnDestroy()
        {
            EditorApplication.update -= OnUpdate;
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

            List<(int idx, string name)> definesToAdd = null;
            for (int n = 0, len = scriptingDefinesProperty.arraySize; n < len; ++n)
            {
                var elementThatNeedsToBePresent = scriptingDefinesProperty.GetArrayElementAtIndex(n);

                string defineThatNeedsToBePresent = elementThatNeedsToBePresent.stringValue;
                if (!ArrayPropertyContains(_enabledScriptDefinesProperty, nameof(BuildTemplate.ScriptDefine.DefineName), defineThatNeedsToBePresent))
                {
                    definesToAdd ??= new List<(int idx, string name)>();
                    definesToAdd.Add((n, defineThatNeedsToBePresent));
                }
            }

            if (definesToAdd != null && definesToAdd.Count > 0)
            {
                for (int i = 0, iLen = definesToAdd.Count; i < iLen; ++i)
                {
                    (int idxItNeedsToBeIn, string defineToAdd) = definesToAdd[i];

                    int addedElementIdx = _enabledScriptDefinesProperty.arraySize;
                    _enabledScriptDefinesProperty.InsertArrayElementAtIndex(addedElementIdx);
                    var addedElement = _enabledScriptDefinesProperty.GetArrayElementAtIndex(addedElementIdx);
                    var addedElementName =
                        addedElement.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.DefineName));
                    addedElementName.stringValue = defineToAdd;
                    var addedElementEnable =
                        addedElement.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.Enable));
                    addedElementEnable.boolValue = true;

                    _enabledScriptDefinesProperty.MoveArrayElement(addedElementIdx, idxItNeedsToBeIn);
                }
            }

            for (int n = 0, len = scriptingDefinesProperty.arraySize; n < len; ++n)
            {
                var elementThatNeedsToBePresent = scriptingDefinesProperty.GetArrayElementAtIndex(n);
                string defineThatNeedsToBePresent = elementThatNeedsToBePresent.stringValue;

                int whereDefineIs = ArrayPropertyIndexOf(_enabledScriptDefinesProperty, nameof(BuildTemplate.ScriptDefine.DefineName), defineThatNeedsToBePresent);

                if (whereDefineIs != n)
                {
                    _enabledScriptDefinesProperty.MoveArrayElement(whereDefineIs, n);
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
        }

        // =================================================================================

        static readonly ReorderableList.HeaderCallbackDelegate DrawSceneListHeader = _DrawSceneListHeader;
        static void _DrawSceneListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Scenes");
        }

        ReorderableList.FooterCallbackDelegate DrawSceneFooter => _DrawSceneListFooter;
        void _DrawSceneListFooter(Rect rect)
        {
            // to draw the plus & minus buttons:
            ReorderableList.defaultBehaviours.DrawFooter(rect, _scenesReorderableList);

            const int width = 98;
            const int plusMinusButtonsWidth = 80;
            if (UnityEngine.Event.current.type == UnityEngine.EventType.Repaint)
            {
                var rectBg = new Rect(rect.xMax - plusMinusButtonsWidth - width, rect.y, width + 8, rect.height);
                ReorderableList.defaultBehaviours.footerBackground.Draw(rectBg, false, false, false, false);
            }

            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled &&
                          _sceneListProperty.objectReferenceValue != null &&
                          ((SceneList) _sceneListProperty.objectReferenceValue).HasAtLeastOneLoadableScene &&
                          _scenesProperty != null && _scenesProperty.arraySize > 0;

            Rect loadScenesRect = new Rect(rect.xMax - plusMinusButtonsWidth - width + 4, rect.y, width, 16f);
            if (GUI.Button(loadScenesRect, _loadScenes, ReorderableList.defaultBehaviours.preButton))
            {
                if (EditorUtility.DisplayDialog("Load all scenes?",
                    "This will load all scenes in the Scene List, and close all currently opened scenes that are not in the list.",
                    "Yes", "No") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    _wantToLoad = true;
                }
            }

            GUI.enabled = prevEnabled;
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

            var addedElement = _scenesProperty.GetArrayElementAtIndex(idx);
            addedElement.objectReferenceValue = null;
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


        ReorderableList.ElementCallbackDelegate DrawSceneElementBg => _DrawSceneElementBg;
        void _DrawSceneElementBg(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var element = _scenesReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            int firstIdx = ArrayPropertyIndexOf(_scenesProperty, element.objectReferenceValue);
            bool duplicate = firstIdx != -1 && firstIdx != index;

            var bgStyle = duplicate
                ? _skin.FindStyle("RL Warning Background")
                : ReorderableList.defaultBehaviours.elementBackground;
            bgStyle.Draw(rect, false, isActive, isActive, isFocused);
        }

        ReorderableList.ElementCallbackDelegate DrawSceneElement => _DrawSceneElement;
        void _DrawSceneElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _scenesReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            int firstIdx = ArrayPropertyIndexOf(_scenesProperty, element.objectReferenceValue);
            bool duplicate = firstIdx != -1 && firstIdx != index;

            CalcSize.text = "99";
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(CalcSize).x + 5;

            _label.image = null;
            _label.text = index.ToString();

            var sceneRect = new Rect(rect.x, rect.y + 2, rect.width, rect.height - 4);
            if (duplicate)
            {
                sceneRect.xMax -= 26;
            }
            EditorGUI.PropertyField(sceneRect, element, _label);

            if (duplicate)
            {
                GUI.Label(new Rect(rect.xMax - 20, rect.y + 2, 20, 19), _duplicateSceneLabel);
            }
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

        ReorderableList.ElementCallbackDelegate DrawScriptDefineElementBg => _DrawScriptDefineElementBg;
        void _DrawScriptDefineElementBg(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (Event.current.type != EventType.Repaint || index < 0)
            {
                return;
            }

            var element = _enabledScriptDefinesProperty.GetArrayElementAtIndex(index);
            var nameProperty = element.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.DefineName));

            int firstIdx = ArrayPropertyIndexOf(_enabledScriptDefinesProperty,
                nameof(BuildTemplate.ScriptDefine.DefineName), nameProperty.stringValue);
            bool duplicate = firstIdx != -1 && firstIdx != index;

            var bgStyle = duplicate
                ? _skin.FindStyle("RL Warning Background")
                : ReorderableList.defaultBehaviours.elementBackground;
            bgStyle.Draw(rect, false, isActive, isActive, isFocused);
        }

        ReorderableList.ElementCallbackDelegate DrawScriptDefineElement => _DrawScriptDefineElement;
        void _DrawScriptDefineElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _enabledScriptDefinesProperty.GetArrayElementAtIndex(index);

            var enabledProperty = element.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.Enable));
            var nameProperty = element.FindPropertyRelative(nameof(BuildTemplate.ScriptDefine.DefineName));

            SerializedObject scriptingDefineListObject;
            SerializedProperty scriptingDefinesProperty;
            if (_scriptingDefineListProperty.objectReferenceValue != null)
            {
                scriptingDefineListObject = new SerializedObject(_scriptingDefineListProperty.objectReferenceValue);
                scriptingDefinesProperty = scriptingDefineListObject.FindProperty(nameof(ScriptingDefineList.ScriptingDefines));
            }
            else
            {
                scriptingDefineListObject = null;
                scriptingDefinesProperty = null;
            }

            int idxInScriptingDefineList = -1;
            if (scriptingDefineListObject != null && scriptingDefinesProperty != null)
            {
                idxInScriptingDefineList = ArrayPropertyIndexOf(scriptingDefinesProperty, nameProperty.stringValue);
            }

            int firstIdx = ArrayPropertyIndexOf(_enabledScriptDefinesProperty,
                nameof(BuildTemplate.ScriptDefine.DefineName), nameProperty.stringValue);
            bool duplicate = firstIdx != -1 && firstIdx != index;

            // ------------------------------------------------------

            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), enabledProperty, GUIContent.none);

            var nameRect = new Rect(rect.x + 20, rect.y + 2, rect.width - 20, rect.height - 4);
            if (idxInScriptingDefineList == -1)
            {
                nameRect.width -= 26;
            }

            if (duplicate)
            {
                nameRect.width -= 26;
            }

            var prevName = nameProperty.stringValue;
            EditorGUI.PropertyField(nameRect, nameProperty, GUIContent.none);
            if (prevName != nameProperty.stringValue &&
                scriptingDefineListObject != null &&
                scriptingDefinesProperty != null &&
                idxInScriptingDefineList == index)
            {
                scriptingDefineListObject.Update();

                var elementOther = scriptingDefinesProperty.GetArrayElementAtIndex(index);
                elementOther.stringValue = nameProperty.stringValue;

                scriptingDefineListObject.ApplyModifiedProperties();
            }

            if (idxInScriptingDefineList == -1 && scriptingDefineListObject != null && scriptingDefinesProperty != null)
            {
                var plusRect = new Rect(rect.xMax - 20, rect.y + 2, 20, 19);
                if (duplicate)
                {
                    plusRect.x -= 26;
                }

                if (GUI.Button(plusRect, _addDefineLabel, BuildFrontend.Styles.MiniIconButton))
                {
                    scriptingDefineListObject.Update();

                    int addIdx = scriptingDefinesProperty.arraySize;
                    scriptingDefinesProperty.InsertArrayElementAtIndex(addIdx);
                    var addedElement = scriptingDefinesProperty.GetArrayElementAtIndex(addIdx);
                    addedElement.stringValue = nameProperty.stringValue;

                    scriptingDefineListObject.ApplyModifiedProperties();
                }
            }

            if (duplicate)
            {
                GUI.Label(new Rect(rect.xMax - 20, rect.y + 2, 20, 19), _duplicateDefineLabel);
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

        bool _wantToLoad;

        UnityEditor.EditorApplication.CallbackFunction OnUpdate => _OnUpdate;
        void _OnUpdate()
        {
            if (_wantToLoad)
            {
                _wantToLoad = false;
                var sceneList = (SceneList) _sceneListProperty.objectReferenceValue;
                EditorSceneManager.RestoreSceneManagerSetup(sceneList.SceneSetup);
            }
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