using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class SDFViewerEditWindow : EditorWindow
    {
        #region ----RTHandle----
        private RTHandle m_virtualCamColorHandle;

        private void SetupHandle()
        {
            m_virtualCamColorHandle = new RTHandle(1024, 1024, 0, GraphicsFormat.R8G8B8A8_UNorm);
        }

        private void ReleaseHandle()
        {
            m_virtualCamColorHandle?.Release();
            m_virtualCamColorHandle = null;
        }
        #endregion

        #region ----Material----
        private Material m_sdfViewerMat;

        private void SetupMaterial()
        {
            m_sdfViewerMat = new Material(Shader.Find("ACloud/Editor/SDFViewer"));
        }

        private void ReleaseMaterial()
        {
            if (m_sdfViewerMat != null)
            {
                DestroyImmediate(m_sdfViewerMat);
            }
        }
        #endregion

        #region ----Shader Helper----
        private readonly static int k_shaderProperty_Float_CameraZoom = Shader.PropertyToID("_CameraZoom");
        private readonly static int k_shaderProperty_Float_SDFExitEpsilon = Shader.PropertyToID("_SDFExitEpsilon");
        private readonly static int k_shaderProperty_Vec_CameraWorldPos = Shader.PropertyToID("_CameraWorldPos");
        private readonly static int k_shaderProperty_Vec_AABBMax = Shader.PropertyToID("_AABBMax");
        private readonly static int k_shaderProperty_Vec_AABBMin = Shader.PropertyToID("_AABBMin");
        private readonly static int k_shaderProperty_Mat_VCam_CameraToWorldMatrix = Shader.PropertyToID("_VCam_CameraToWorldMatrix");
        private readonly static int k_shaderProperty_Tex_VolumeSDFTex = Shader.PropertyToID("_VolumeSDFTex");
        #endregion

        #region ----Transforms----
        //Demo里就"简单"点
        private Vector3 m_VCam_Position;
        private Quaternion m_VCam_Rotation;
        private Vector3 m_VCam_EulerAngle;

        private float m_VCam_MoveSpeed;

        private float m_VCam_Zoom;

        private Matrix4x4 m_VCam_CameraToWorld;

        private Vector3 m_VCam_Forward
        {
            get
            {
                return m_VCam_Rotation * Vector3.forward;
            }
        }

        private Vector3 m_VCam_Up
        {
            get
            {
                return m_VCam_Rotation * Vector3.up;
            }
        }

        private Vector3 m_VCam_Right
        {
            get
            {
                return m_VCam_Rotation * Vector3.right;
            }
        }

        private void LookAt(Vector3 desPos, Vector3 sourcePos, ref Quaternion sourceRot)
        {
            Vector3 forward = (desPos - sourcePos).normalized;
            if (forward.magnitude < 10e-8)
            {
                forward = Vector3.forward;
            }
            sourceRot = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void SetupDefaultTransforms()
        {
            m_VCam_MoveSpeed = 200;
            //
            float _maxSize = m_AABB_Scale * m_AABB_BaseSize.magnitude * 0.5f;
            float _sinHalfTheta = Mathf.Sin(0.5f * Mathf.Deg2Rad * 60);
            m_VCam_Zoom = Mathf.Tan(0.5f * Mathf.Deg2Rad * 60);
            float _distance = _maxSize / (_sinHalfTheta);
            m_VCam_Position = Vector3.one * _distance;
            LookAt(Vector3.zero, m_VCam_Position, ref m_VCam_Rotation);
            //
            m_VCam_EulerAngle = m_VCam_Rotation.eulerAngles;
        }

        private void CalculateVirtualCameraMatrices()
        {
            m_VCam_CameraToWorld = Matrix4x4.TRS(m_VCam_Position, m_VCam_Rotation, Vector3.one);
        }
        #endregion

        #region ----Virtual Camera Control----
        private Vector2 m_lastFrameRightMousePosition;
        private Vector2 m_lastFrameMiddleMousePosition;

        private double m_lastFrameTime;

        private void OnCamRotate(Vector2 delta)
        {
            delta *= -0.1f;
            m_VCam_EulerAngle += new Vector3(delta.y, delta.x, 0);
            //
            m_VCam_Rotation = Quaternion.Euler(m_VCam_EulerAngle);
        }

        private void OnCamMove(Vector2 delta)
        {
            delta *= 0.01f;
            //移动相机
            m_VCam_Position += (-delta.x * m_VCam_Right + delta.y * m_VCam_Up);
        }

        private void OnControlVirtualCamera(Rect sceneViewRect)
        {
            float deltaTime = (float)(EditorApplication.timeSinceStartup - m_lastFrameTime);
            Vector2 _mousePosition = Event.current.mousePosition;

            bool isCursorInRect = sceneViewRect.Contains(_mousePosition);
            //鼠标在纹理绘制区域内
            if ((Event.current.type == EventType.MouseDown) && isCursorInRect)
            {
                if (Event.current.isMouse)
                {
                    if (Event.current.button == 1)
                    {
                        //鼠标右键
                        m_lastFrameRightMousePosition = _mousePosition;
                    }
                    else if (Event.current.button == 2)
                    {
                        //鼠标中键
                        m_lastFrameMiddleMousePosition = _mousePosition;
                    }
                }
            }
            else if ((Event.current.type == EventType.MouseDrag) && isCursorInRect)
            {
                if (Event.current.isMouse)
                {
                    if (Event.current.button == 1)
                    {
                        //鼠标右键
                        Vector2 deltaMousePosition = _mousePosition - m_lastFrameRightMousePosition;
                        OnCamRotate(deltaMousePosition);
                        m_isNeedRepaint = true;
                        m_lastFrameRightMousePosition = _mousePosition;
                    }
                    else if (Event.current.button == 2)
                    {
                        //鼠标中键
                        Vector2 deltaMousePosition = _mousePosition - m_lastFrameMiddleMousePosition;
                        OnCamMove(deltaMousePosition);
                        m_isNeedRepaint = true;
                        m_lastFrameMiddleMousePosition = _mousePosition;
                    }
                }
            }
            if (Event.current.Equals(Event.KeyboardEvent("W")))//W
            {
                //ref: https://docs.unity3d.com/ScriptReference/Event.KeyboardEvent.html
                m_VCam_Position += m_VCam_Forward * m_VCam_MoveSpeed * deltaTime;
                m_isNeedRepaint = true;
            }
            if (Event.current.Equals(Event.KeyboardEvent("A")))//A
            {
                m_VCam_Position -= m_VCam_Right * m_VCam_MoveSpeed * deltaTime;
                m_isNeedRepaint = true;
            }
            if (Event.current.Equals(Event.KeyboardEvent("S")))//S
            {
                m_VCam_Position -= m_VCam_Forward * m_VCam_MoveSpeed * deltaTime;
                m_isNeedRepaint = true;
            }
            if (Event.current.Equals(Event.KeyboardEvent("D")))//D
            {
                m_VCam_Position += m_VCam_Right * m_VCam_MoveSpeed * deltaTime;
                m_isNeedRepaint = true;
            }
            if (Event.current.Equals(Event.KeyboardEvent("E")))//E
            {
                m_VCam_Position += m_VCam_Up * m_VCam_MoveSpeed * deltaTime;
                m_isNeedRepaint = true;
            }
            if (Event.current.Equals(Event.KeyboardEvent("Q")))//Q
            {
                m_VCam_Position -= m_VCam_Up * m_VCam_MoveSpeed * deltaTime;
                m_isNeedRepaint = true;
            }
            //
            m_lastFrameTime = EditorApplication.timeSinceStartup;
        }
        #endregion

        #region ----Virtual Camera Rendering----
        private void OnRenderingEditorVirtualCamera()
        {
            CalculateVirtualCameraMatrices();
            //
            CommandBuffer editCmdBuffer = new CommandBuffer()
            {
                name = "SDF View Pass"
            };
            editCmdBuffer.SetRenderTarget(m_virtualCamColorHandle);
            editCmdBuffer.ClearRenderTarget(false, true, Color.clear);
            //
            if (s_selectTex3D != null)
            {
                MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
                matPropertyBlock.SetFloat(k_shaderProperty_Float_CameraZoom, m_VCam_Zoom);
                matPropertyBlock.SetFloat(k_shaderProperty_Float_SDFExitEpsilon, m_Marching_ExitEpsilon);
                matPropertyBlock.SetVector(k_shaderProperty_Vec_CameraWorldPos, m_VCam_Position);
                matPropertyBlock.SetVector(k_shaderProperty_Vec_AABBMax, m_AABB_Scale * new Vector4(0.5f * m_AABB_BaseSize.x, 0.5f * m_AABB_BaseSize.y, 0.5f * m_AABB_BaseSize.z, 1));
                matPropertyBlock.SetVector(k_shaderProperty_Vec_AABBMin, m_AABB_Scale * new Vector4(-0.5f * m_AABB_BaseSize.x, -0.5f * m_AABB_BaseSize.y, -0.5f * m_AABB_BaseSize.z, 1));
                matPropertyBlock.SetMatrix(k_shaderProperty_Mat_VCam_CameraToWorldMatrix, m_VCam_CameraToWorld);
                matPropertyBlock.SetTexture(k_shaderProperty_Tex_VolumeSDFTex, s_selectTex3D);
                editCmdBuffer.DrawProcedural(Matrix4x4.identity, m_sdfViewerMat, 0, MeshTopology.Triangles, 3, 1, matPropertyBlock);
            }
            //
            Graphics.ExecuteCommandBuffer(editCmdBuffer);
        }
        #endregion
    }
#endif
}