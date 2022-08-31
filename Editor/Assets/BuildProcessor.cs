using System;
using UnityEngine;

namespace BuildFrontend
{
    public abstract class BuildProcessor: ScriptableObject
    {
        public abstract bool OnPreProcess(BuildTemplate template, bool wantRun);
        public abstract bool OnPostProcess(BuildTemplate template, UnityEditor.Build.Reporting.BuildReport report, bool wantRun);
    }

    public class BuildProcessorException: Exception
    {
        public BuildProcessor processor;
        public BuildTemplate template;
        public BuildProcessorException(BuildProcessor p, BuildTemplate t)
        {
            processor = p;
            template = t;
        }
    }
}