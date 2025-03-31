using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(CloudManager))]
    public class EditCloudManager : UnityEditor.Editor
    {
        private CloudManager m_CloudManager;
        private bool m_isBuilding;

        #region ----Unity----
        private void OnEnable()
        {
            m_CloudManager = (CloudManager)target;
            m_isBuilding = false;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUI.BeginDisabledGroup(m_isBuilding);
            {
                if (GUILayout.Button(new GUIContent("Pre Build Cloud Shape Bundle", "组合云的形状")))
                {
                    m_CloudManager.OnPreBuildCloudShapeBundle(ref m_isBuilding);
                }
            }
            EditorGUI.EndDisabledGroup();
        }
        #endregion
    }
#endif
}