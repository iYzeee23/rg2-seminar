using UnityEngine;

// renderuje scenu iz perspektive svetla u RenderTexture (shadow mapu)
// koristi ShadowCaster.shader kao replacement shader na svim objektima

// reference:
// Williams (1978) https://dl.acm.org/doi/10.1145/965139.807402
// Donnelly & Lauritzen (2006) https://dl.acm.org/doi/10.1145/1111411.1111440

// korisni materijali iz dokumentacije:
// https://docs.unity3d.com/ScriptReference/Camera.SetReplacementShader.html
// https://docs.unity3d.com/ScriptReference/RenderTextureFormat.RGFloat.html
// https://docs.unity3d.com/ScriptReference/Shader.SetGlobalTexture.html
// https://docs.unity3d.com/Manual/SL-ShaderReplacement.html

public class ShadowMapCamera : MonoBehaviour
{
    [Header("Shadow Map Settings")]
    [SerializeField] private int shadowMapResolution = 1024;
    [SerializeField] private float shadowDistance = 30f;
    [SerializeField] private Shader shadowCasterShader;

    private Camera shadowCamera;
    private RenderTexture shadowMap;

    public RenderTexture ShadowMap => shadowMap;
    public Camera ShadowCam => shadowCamera;

    void Start()
    {
        // RGFloat: R = depth (prvi moment), G = depth^2 (drugi moment, samo za VSM)
        shadowMap = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.RGFloat);

        // Point default - za VSM se prebacuje na Bilinear
        // Basic SM/PCF ne smeju da interpoliraju sirove dubine
        shadowMap.filterMode = FilterMode.Point;
        shadowMap.wrapMode = TextureWrapMode.Clamp;
        shadowMap.Create();

        shadowCamera = GetComponent<Camera>();
        if (shadowCamera == null)
            shadowCamera = gameObject.AddComponent<Camera>();

        // rucno renderujemo iz RenderShadowMap()
        shadowCamera.enabled = false;

        // ortografska za directional light (paralelni zraci)
        shadowCamera.orthographic = true;
        shadowCamera.orthographicSize = shadowDistance / 2f;
        shadowCamera.nearClipPlane = 0.1f;
        shadowCamera.farClipPlane = shadowDistance * 2f;
        shadowCamera.targetTexture = shadowMap;
        shadowCamera.clearFlags = CameraClearFlags.SolidColor;

        // bela = max dubina (nema senke)
        shadowCamera.backgroundColor = Color.white;
        shadowCamera.cullingMask = ~0;

        // svi objekti se renderuju sa ShadowCaster.shader umesto svojih shadera
        // Unity matchuje po "RenderType" tagu
        if (shadowCasterShader != null)
            shadowCamera.SetReplacementShader(shadowCasterShader, "RenderType");
    }

    // Point za Basic/PCF, Bilinear za VSM
    // momenti (depth, depth^2) su filterabilni jer je srednja vrednost linearna: E(aX+bY) = aE(X)+bE(Y)
    public void SetFilterMode(FilterMode mode)
    {
        if (shadowMap != null)
            shadowMap.filterMode = mode;
    }

    void OnDestroy()
    {
        if (shadowMap != null) shadowMap.Release();
    }

    public void RenderShadowMap(Light light)
    {
        // postavi kameru u poziciju svetla
        Vector3 lightDir = light.transform.forward;
        transform.position = -lightDir * shadowDistance * 0.5f;
        transform.rotation = Quaternion.LookRotation(lightDir);

        // globalne varijable za sve shadow receiver shadere
        Shader.SetGlobalVector("_LightPosition", transform.position);
        Shader.SetGlobalVector("_LightParams", new Vector4(
            shadowCamera.nearClipPlane,
            shadowCamera.farClipPlane, 0, 0));

        // VP matrica svetla: world -> clip iz ugla svetla
        // receiver shaderi se koriste za shadow map lookup
        Matrix4x4 lightVP = shadowCamera.projectionMatrix * shadowCamera.worldToCameraMatrix;
        Shader.SetGlobalMatrix("_LightViewProjection", lightVP);

        // renderuj shadow mapu (svi objekti se crtaju sa ShadowCaster shaderom)
        shadowCamera.Render();
        Shader.SetGlobalTexture("_ShadowMap", shadowMap);
    }
}
