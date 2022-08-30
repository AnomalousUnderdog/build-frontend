using System;
using UnityEditor;

namespace BuildFrontend
{
    public class BuildProfile : BuildFrontendAssetBase
    {
        public BuildTarget Target;

        public bool DetailedBuildReport;

        [Serializable]
        public enum BuildCompressionMethod
        {
            Default,
            LZ4,
            LZ4HighCompression
        }

        public BuildCompressionMethod CompressionMethod = BuildCompressionMethod.Default;

        public bool CopyPDBFiles;
        public bool DevelopmentBuild;
        public bool AutoConnectProfiler;
        public bool DeepProfiling;
        public bool AllowScriptDebugging;
        public bool ShaderLiveLink;
    }
}