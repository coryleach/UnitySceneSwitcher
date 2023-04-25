using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Editor.SceneSwitcher
{
    public class SceneSwitcherWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Window/SceneSwitcher")]
        private static void OpenWindow()
        {
            var window = GetWindow<SceneSwitcherWindow>(false, "Scenes", true);
            window.Show();
        }

        private Vector2 _scrollPosition;

        [System.Serializable]
        private class SceneSwitcherData
        {
            public List<SceneData> scenes = new List<SceneData>();
            public bool sortRecentToTop = true;
            public bool loadAdditive = true;
            public bool closeScenes = true;

            public void AddScene(string guid)
            {
                if (!scenes.Exists((scene) => scene.guid == guid))
                {
                    var sceneData = new SceneData() { guid = guid, color = Color.white };

                    scenes.Add(sceneData);
                }
            }

            [System.Serializable]
            internal class SceneData
            {
                public string guid;
                public Color color;
                public bool foldout;
                public BooleanOverride loadAdditive;
                public BooleanOverride closeScenes;
            }
        }

        public enum  BooleanOverride
        {
            Default = 0,
            Yes = 1,
            No = 2
        }

        private SceneSwitcherData _sceneSwitcherData = new SceneSwitcherData();

        private const string PrefsKey = "EditorSceneSwitcher";

        private void OnEnable()
        {
            //Load State
            var jsonData = PlayerPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(jsonData))
            {
                return;
            }

            _sceneSwitcherData = JsonUtility.FromJson<SceneSwitcherData>(jsonData);
        }

        private void OnDisable()
        {
            SaveState();
        }

        private void SaveState()
        {
            //Save State
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_sceneSwitcherData));
        }

        private bool _editing;

        private void OnGUI()
        {
            if (Application.isPlaying)
            {
                var scene = SceneManager.GetActiveScene();
                GUILayout.Label("Scene Switching Disabled While Playing");
                GUILayout.Label($"Active Scene: {scene.name}");
                GUILayout.Label($"Total Loaded Scenes: {SceneManager.sceneCount}");
                return;
            }

            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = IsValidSelection(DragAndDrop.objectReferences)
                        ? DragAndDropVisualMode.Generic
                        : DragAndDropVisualMode.Rejected;
                    break;
                case EventType.DragPerform:
                    AddObjects(DragAndDrop.objectReferences);
                    return;
                case EventType.DragExited:
                    break;
                default:
                    //Other events ignored
                    break;
            }

            if (_sceneSwitcherData.scenes.Count > 0)
            {
                SceneListGui();
            }
            else
            {
                BoxGui("Drop Scene Assets Here");
            }
        }

        private static void BoxGui(string text)
        {
            var rect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            const int padding = 10;
            rect.x += padding;
            rect.y += padding;
            rect.width -= padding * 2;
            rect.height -= padding * 2;

            var boxStyle = new GUIStyle("GroupBox");
            GUI.Box(rect, text, boxStyle);
        }

        private void SceneListGui()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            const int lineHeight = 18;

            GUILayout.BeginVertical(new GUIStyle("GroupBox"));
            foreach (var sceneData in _sceneSwitcherData.scenes)
            {
                if (sceneData.foldout)
                {
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                }
                else
                {
                    GUILayout.BeginVertical();
                }

                GUILayout.BeginHorizontal();

                if (_editing)
                {
                    if (GUILayout.Button("↑", GUILayout.MaxWidth(20)))
                    {
                        MoveUp(sceneData);
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        break;
                    }
                    else if (GUILayout.Button("↓", GUILayout.MaxWidth(20)))
                    {
                        MoveDown(sceneData);
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        break;
                    }
                }

                var path = AssetDatabase.GUIDToAssetPath(sceneData.guid);
                var preColorBG = GUI.backgroundColor;
                GUI.backgroundColor = sceneData.color;

                if (GUILayout.Button(System.IO.Path.GetFileNameWithoutExtension(path)))
                {
                    GUI.backgroundColor = preColorBG;
                    // Give user option to save/cancel
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        break;
                    }

                    SwitchToScene(sceneData, path);
                    if (_sceneSwitcherData.sortRecentToTop)
                    {
                        MoveToTop(sceneData);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    break;
                }

                GUI.backgroundColor = preColorBG;

                if (_editing)
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_TreeEditor.Trash"), GUILayout.Width(30)))
                    {
                        _sceneSwitcherData.scenes.Remove(sceneData);
                        SaveState();
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        break;
                    }
                    GUI.backgroundColor = Color.white;

                    var arrowTexture = sceneData.foldout ? EditorGUIUtility.IconContent("d_scrolldown") : EditorGUIUtility.IconContent("d_scrollup");
                    if (GUILayout.Button(arrowTexture, GUILayout.Width(25), GUILayout.Height(20)))
                    {
                        sceneData.foldout = !sceneData.foldout;
                    }
                }

                GUILayout.EndHorizontal();

                if (_editing && sceneData.foldout)
                {
                    GUILayout.BeginVertical();

                    var temp = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 100;

                    sceneData.color = EditorGUILayout.ColorField("Button Tint",sceneData.color);
                    sceneData.loadAdditive = (BooleanOverride)EditorGUILayout.EnumPopup(new GUIContent("Additive Load", "Loads scenes additively"),sceneData.loadAdditive);
                    sceneData.closeScenes = (BooleanOverride)EditorGUILayout.EnumPopup(new GUIContent("Close Others", "Will close/unload other scenes when additive loading is active"),sceneData.closeScenes);

                    EditorGUIUtility.labelWidth = temp;

                    GUILayout.EndVertical();
                }

                GUILayout.EndVertical();

            }

            GUILayout.EndVertical();

            if (_editing)
            {
                //Draw Toggle Buttons
                GUILayout.Label("Default Settings");
                GUILayout.BeginHorizontal(new GUIStyle("GroupBox"));
                _sceneSwitcherData.sortRecentToTop = GUILayout.Toggle(_sceneSwitcherData.sortRecentToTop,
                    new GUIContent("Auto Sort", "Will sort most recently used scenes to the top"),
                    GUILayout.Height(lineHeight));
                _sceneSwitcherData.loadAdditive = GUILayout.Toggle(_sceneSwitcherData.loadAdditive,
                    new GUIContent("Additive", "Loads scenes additively"), GUILayout.Height(lineHeight));
                _sceneSwitcherData.closeScenes = GUILayout.Toggle(_sceneSwitcherData.closeScenes,
                    new GUIContent("Close", "Will close/unload other scenes when additive loading is active"),
                    GUILayout.Height(lineHeight));
                GUILayout.EndHorizontal();

                //Draw Done Button
                GUILayout.BeginHorizontal();

                GUILayout.FlexibleSpace();
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Done", GUILayout.MaxWidth(60)))
                {
                    _editing = !_editing;
                }
                GUI.backgroundColor = Color.white;

                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.Space();

            }

            EditorGUILayout.EndScrollView();
        }

        private void MoveToTop(SceneSwitcherData.SceneData guid)
        {
            _sceneSwitcherData.scenes.Remove(guid);
            _sceneSwitcherData.scenes.Insert(0, guid);
        }

        private void MoveUp(SceneSwitcherData.SceneData guid)
        {
            var index = _sceneSwitcherData.scenes.IndexOf(guid);
            _sceneSwitcherData.scenes.RemoveAt(index);
            if (index > 0)
            {
                index--;
            }

            _sceneSwitcherData.scenes.Insert(index, guid);
        }

        private void MoveDown(SceneSwitcherData.SceneData guid)
        {
            var index = _sceneSwitcherData.scenes.IndexOf(guid);
            _sceneSwitcherData.scenes.RemoveAt(index);
            if (index < _sceneSwitcherData.scenes.Count)
            {
                index++;
            }

            _sceneSwitcherData.scenes.Insert(index, guid);
        }

        private void SwitchToScene(SceneSwitcherData.SceneData sceneData, string path)
        {
            var closeScenes = sceneData.closeScenes == BooleanOverride.Default ? _sceneSwitcherData.closeScenes : (sceneData.closeScenes == BooleanOverride.Yes);
            var loadAdditively = sceneData.loadAdditive == BooleanOverride.Default ? _sceneSwitcherData.loadAdditive : (sceneData.loadAdditive == BooleanOverride.Yes);

            var scene = EditorSceneManager.OpenScene(path, loadAdditively ? OpenSceneMode.Additive : OpenSceneMode.Single);

            if (!closeScenes)
            {
                return;
            }

            //Close other scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var otherScene = SceneManager.GetSceneAt(i);
                if (otherScene.isLoaded && otherScene != scene)
                {
                    EditorSceneManager.CloseScene(otherScene, false);
                }
            }
        }

        private void AddObjects(Object[] objects)
        {
            foreach (var obj in objects)
            {
                var sceneAsset = obj as SceneAsset;

                if (sceneAsset == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(sceneAsset);
                var guid = AssetDatabase.AssetPathToGUID(path);

                _sceneSwitcherData.AddScene(guid);
            }
            SaveState();
        }

        private static bool IsValidSelection(IEnumerable<Object> objects)
        {
            return objects.Select(t => t as SceneAsset).All(sceneAsset => sceneAsset != null);
        }

        #region  IHasCustomMenu Implementation

        public void AddItemsToMenu(GenericMenu menu)
        {
            GUIContent content = new GUIContent("Edit Mode");
            menu.AddItem(content, _editing, ToggleEdit);
        }

        private void ToggleEdit()
        {
            _editing = !_editing;
        }

        #endregion

    }
}
