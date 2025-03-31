using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Callbacks;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class SDFViewerEditWindow : EditorWindow
    {
        #region ----Command----
        public static void InitWindow()
        {
            CurrentWin = GetWindowWithRect<SDFViewerEditWindow>(new Rect(0, 0, k_GUI_WindowWidth, k_GUI_WindowHeight));
            CurrentWin.titleContent = new GUIContent("SDFViewer", "查看3D SDF纹理");
            CurrentWin.Focus();
        }

        [MenuItem("ACloud/SDF/SDFViewer")]
        public static void OpenWindowFromMenu()
        {
            InitWindow();
            s_selectTex3D = null;
        }

        [OnOpenAsset()]
        public static bool OpenWindowFromAsset(int instanceID, int line)
        {
            string assetPath = AssetDatabase.GetAssetPath(instanceID);
            Type assetType = AssetDatabase.GetImporterType(assetPath);
            //
            if (assetType == typeof(AssetImporter))
            {
                Type uniAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (uniAssetType == typeof(Texture3D))
                {
                    s_selectTex3D = AssetDatabase.LoadAssetAtPath<Texture3D>(assetPath);
                    InitWindow();
                    return true;
                }
            }
            //
            return false;
        }
        #endregion

        #region ----Static Properties----
        public static SDFViewerEditWindow CurrentWin { get; private set; }

        private static Texture3D s_selectTex3D;
        #endregion

        #region ----GUI Properties----
        private Vector2 m_scrollPosition;

        private bool m_isNeedRepaint;

        
        private Vector3Int m_AABB_BaseSize;
        private float m_AABB_Scale;

        private float m_Marching_ExitEpsilon;

        private void SetInitProperties()
        {
            m_isNeedRepaint = true;
            if (s_selectTex3D != null)
            {
                int w = s_selectTex3D.width;
                int h = s_selectTex3D.height;
                int d = s_selectTex3D.depth;
                int min = Mathf.Min(w, h, d);
                m_AABB_BaseSize = new Vector3Int(w / min, h / min, d / min);
            }
            else
            {
                m_AABB_BaseSize = Vector3Int.one;
            }
            m_AABB_Scale = 1;
            m_Marching_ExitEpsilon = 0.1f;
        }
        #endregion

        #region ----GUI Constants----
        private const float k_GUI_WindowWidth = 1020;
        private const float k_GUI_WindowHeight = 730;
        private const float k_GUI_EditPanel_ScrollViewWidth = 290;

        private const int k_GUI_SceneViewTex_Width = 720;
        private const int k_GUI_SceneViewTex_Height = 720;
        #endregion

        #region ----GUI----
        private void DrawSetting()
        {
            GUILayout.Space(5);
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.Width(k_GUI_EditPanel_ScrollViewWidth));
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            //
            EditorGUI.BeginChangeCheck();
            //
            s_selectTex3D = EditorGUILayout.ObjectField("目标纹理", s_selectTex3D, typeof(Texture3D), true) as Texture3D;
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            m_AABB_BaseSize = EditorGUILayout.Vector3IntField("基础比例:", m_AABB_BaseSize);
            m_AABB_Scale = EditorGUILayout.Slider("Scale:", m_AABB_Scale, 0.1f, 5f);
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
            m_Marching_ExitEpsilon = EditorGUILayout.Slider("Ray Marching Exit Epsilon:", m_Marching_ExitEpsilon, 0.001f, 1f);
            //
            if (EditorGUI.EndChangeCheck())
            {
                m_isNeedRepaint = true;
            }
            //
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneView(out Rect sceneViewRect)
        {
            //
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(k_GUI_SceneViewTex_Width + 10), GUILayout.MaxHeight(k_GUI_SceneViewTex_Height + 10));
            sceneViewRect = GUILayoutUtility.GetRect(k_GUI_SceneViewTex_Width, k_GUI_SceneViewTex_Height, GUILayout.ExpandHeight(false), GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(sceneViewRect, m_virtualCamColorHandle, null, ScaleMode.StretchToFill);
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region ----Unity----
        private void OnEnable()
        {
            SetupMaterial();
            SetupHandle();
            SetInitProperties();
            SetupDefaultTransforms();
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            DrawSetting();
            DrawSceneView(out Rect sceneViewRect);
            GUILayout.EndHorizontal();
            if (m_isNeedRepaint)
            {
                OnRenderingEditorVirtualCamera();
                Repaint();
                m_isNeedRepaint = false;
            }
            //
            OnControlVirtualCamera(sceneViewRect);
        }

        private void OnDisable()
        {
            ReleaseMaterial();
            ReleaseHandle();
        }
        #endregion
    }
#endif
}