using System.Collections.Generic;
using UnityEditor;

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
                    if (scene != null)
                        scenes.Add(AssetDatabase.GetAssetPath(scene));
                }
                return scenes.ToArray();
            }
        }
    }
}