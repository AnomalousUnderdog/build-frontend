using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace BuildFrontend
{
    public class SceneList : BuildFrontendAssetBase
    {
        public SceneAsset[] Scenes;

        protected override void Awake()
        {
            base.Awake();

            Scenes ??= new SceneAsset[0];
        }

        public string[] ScenePaths
        {
            get
            {
                if (Scenes == null)
                {
                    return null;
                }

                var scenes = new List<string>();
                foreach (var scene in Scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }

                    string scenePath = AssetDatabase.GetAssetPath(scene);
                    if (scenes.Contains(scenePath))
                    {
                        // already in list
                        continue;
                    }

                    scenes.Add(scenePath);
                }

                return scenes.ToArray();
            }
        }

        public bool HasAtLeastOneLoadableScene
        {
            get
            {
                foreach (var scene in Scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }
        }

        public SceneSetup[] SceneSetup
        {
            get
            {
                if (Scenes == null)
                {
                    return null;
                }

                var scenes = new List<SceneSetup>();
                foreach (var scene in Scenes)
                {
                    if (scene == null)
                    {
                        continue;
                    }

                    var newEntry = new SceneSetup
                    {
                        path = AssetDatabase.GetAssetPath(scene),
                        isLoaded = true,
                    };
                    scenes.Add(newEntry);
                }

                if (scenes.Count > 0)
                {
                    scenes[0].isActive = true;
                }

                return scenes.ToArray();
            }
        }
    }
}