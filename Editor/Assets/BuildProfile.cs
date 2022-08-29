using UnityEditor;
using UnityEngine;

namespace BuildFrontend
{
    public class BuildProfile : BuildFrontendAssetBase
    {
        [Header("Build Profile")]
        public bool DevPlayer;
        public BuildTarget Target;
    }

}

