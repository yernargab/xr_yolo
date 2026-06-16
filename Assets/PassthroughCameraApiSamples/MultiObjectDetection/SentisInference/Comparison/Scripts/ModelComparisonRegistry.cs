// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [CreateAssetMenu(
        fileName = "ModelComparisonRegistry",
        menuName = "Passthrough Camera Samples/Multi Object Detection/Model Comparison Registry")]
    public class ModelComparisonRegistry : ScriptableObject
    {
        [SerializeField] private List<ModelComparisonProfile> m_profiles = new();

        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < m_profiles.Count; i++)
                {
                    m_profiles[i]?.LogMissingReferenceWarnings();
                    if (IsValid(m_profiles[i]))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public IReadOnlyList<ModelComparisonProfile> GetValidProfiles()
        {
            var profiles = new List<ModelComparisonProfile>();
            for (var i = 0; i < m_profiles.Count; i++)
            {
                m_profiles[i]?.LogMissingReferenceWarnings();
                if (IsValid(m_profiles[i]))
                {
                    profiles.Add(m_profiles[i]);
                }
            }

            return profiles;
        }

        private static bool IsValid(ModelComparisonProfile profile)
        {
            return profile != null && profile.HasRuntimeModel;
        }
    }
}
