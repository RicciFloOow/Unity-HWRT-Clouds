using ACloud;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DemoWaterLikeMirror : MonoBehaviour
{
    #region ----Shader Helper----
    private readonly static int k_shaderProperty_Tex_NormalTex = Shader.PropertyToID("_NormalTex");
    #endregion

    #region ----Renderer Res----
    private RTHandle m_mirrorNormal_Handle;
    private MeshRenderer m_meshRenderer;
    private Material m_material;
    private void SetupRendererRes()
    {
        if (m_meshRenderer == null)
        {
            m_meshRenderer = GetComponent<MeshRenderer>();
        }
        if (m_meshRenderer != null)
        {
            m_material = Instantiate(m_meshRenderer.sharedMaterial);
            m_meshRenderer.sharedMaterial = m_material;
        }
        if (m_material != null)
        {
            m_mirrorNormal_Handle = new RTHandle(1024, 1024, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
            m_material.SetTexture(k_shaderProperty_Tex_NormalTex, m_mirrorNormal_Handle);
        }
    }
    private void ReleaseRendererRes()
    {
        if (m_material != null)
        {
            Destroy(m_material);
        }
        m_mirrorNormal_Handle?.Release();
        m_mirrorNormal_Handle = null;
    }

    private void RenderingWaterLikeMirrorNormal()
    {
        //TODO:
        CommandBuffer cmd = new CommandBuffer()
        {
            name = "Demo Water Like Mirror Normal Pass"
        };
        cmd.SetRenderTarget(m_mirrorNormal_Handle);
        cmd.ClearRenderTarget(false, true, new Color(0.5f, 0.5f, 1f, 1));
        Graphics.ExecuteCommandBuffer(cmd);
    }
    #endregion

    #region ----Unity----
    private void OnEnable()
    {
        SetupRendererRes();
    }
    private void Update()
    {
        RenderingWaterLikeMirrorNormal();
    }
    private void OnDisable()
    {
        ReleaseRendererRes();
    }
    #endregion
}