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

            if (Scenes == null)
                Scenes = new SceneAsset[0];
        }

        public string[] scenePaths
        {
            get
            {
                if (Scenes == null) return null;
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