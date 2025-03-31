using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(CloudShapeBundle))]
    public class EditCloudShapeBundle : UnityEditor.Editor
    {
        private CloudShapeBundle m_CloudShapeBundle;

        #region ----Procedural Cloud Shape----
        private List<Texture3D> m_proceduralCloudShapeList;

        private ReorderableList m_proceduralCloudShapeGUIList;

        private void InitProceduralCloudList()
        {
            m_proceduralCloudShapeList = new List<Texture3D>();
            if (m_CloudShapeBundle.ProceduralShapePathList != null)
            {
                for (int i = 0; i < m_CloudShapeBundle.ProceduralShapePathList.Count; i++)
                {
                    var path = m_CloudShapeBundle.ProceduralShapePathList[i];
                    Texture3D shape = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                    if (shape != null)
                    {
                        m_proceduralCloudShapeList.Add(shape);
                    }
                }
            }
            //
            m_proceduralCloudShapeGUIList = new ReorderableList(m_proceduralCloudShapeList, typeof(Texture3D));
            m_proceduralCloudShapeGUIList.drawHeaderCallback = OnCloudShapeListDrawHeader;
            m_proceduralCloudShapeGUIList.drawElementCallback = OnCloudShapeListDrawElement;
            m_proceduralCloudShapeGUIList.onAddCallback = OnCloudShapeListAdd;
        }

        private void OnCloudShapeListDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Procedural Cloud Shape List: ");
        }

        private void OnCloudShapeListDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            Texture3D shape = m_proceduralCloudShapeGUIList.list[index] as Texture3D;
            m_proceduralCloudShapeGUIList.list[index] = EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), shape, typeof(Texture3D), false);//TODO
        }

        private void OnCloudShapeListAdd(ReorderableList list)
        {
            if (m_proceduralCloudShapeGUIList.list != null)
            {
                m_proceduralCloudShapeGUIList.list.Add(null);
            }
        }

        private void OnCloudShapeListChanged()
        {
            //我们不通过m_proceduralCloudShapeGUIList.onChangedCallback来更新(触发条件太少了)
            //更新CloudShapeBundle
            List<string> paths = new List<string>();
            for (int i = 0; i < m_proceduralCloudShapeGUIList.list.Count; i++)
            {
                var shape = m_proceduralCloudShapeGUIList.list[i] as Texture3D;
                if (shape != null)
                {
                    paths.Add(AssetDatabase.GetAssetPath(shape));
                }
            }
            //
            m_CloudShapeBundle.ProceduralShapePathList = paths;
        }
        #endregion

        #region ----Unity----
        private void OnEnable()
        {
            m_CloudShapeBundle = (CloudShapeBundle)target;
            InitProceduralCloudList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            //TODO:添加专门查看组合好的SDF的查看器(由于"空"的区域的颜色值为0, 当前SDF查看器会因此得到的错误的步进距离)
            EditorGUI.BeginChangeCheck();
            m_proceduralCloudShapeGUIList.DoLayoutList();
            if (EditorGUI.EndChangeCheck())
            {
                OnCloudShapeListChanged();
            }
            //
            if (GUILayout.Button("保存修改"))
            {
                m_CloudShapeBundle.ForceSave();
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void OnDisable()
        {
            m_proceduralCloudShapeList?.Clear();
            m_proceduralCloudShapeList = null;
            m_proceduralCloudShapeGUIList = null;
        }
        #endregion
    }
#endif
}