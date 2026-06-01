using UnityEngine;

// glavni orkestrator demo aplikacije
// kreira scenu proceduralno, menja shadow tehnike u realnom vremenu

// reference:
// Williams (1978) https://dl.acm.org/doi/10.1145/965139.807402
// Reeves et al. (1987) https://dl.acm.org/doi/10.1145/37402.37435
// Donnelly & Lauritzen (2006) https://dl.acm.org/doi/10.1145/1111411.1111440

// korisni materijali iz dokumentacije:
// https://docs.unity3d.com/Manual/class-InputManager.html
// https://docs.unity3d.com/Manual/GUIScriptingGuide.html

public class ShadowController : MonoBehaviour
{
    [Header("Shaders - prevuci iz Assets/Shaders")]
    [SerializeField] private Shader shadowCasterShader;
    [SerializeField] private Shader noShadowShader;
    [SerializeField] private Shader basicShadowShader;
    [SerializeField] private Shader pcfShadowShader;
    [SerializeField] private Shader vsmShadowShader;

    [Header("Light Control")]
    [SerializeField] private float lightRotationSpeed = 20f;

    private Material[] shadowMaterials;
    private string[] techniqueNames;
    private int currentTechnique = 0;

    private Light mainLight;
    private ShadowMapCamera shadowMapCamera;
    private GameObject[] sceneObjects;

    private float fpsTimer = 0;
    private int fpsCount = 0;
    private float currentFPS = 0;

    // H = sakrij/prikazi UI overlay (za screenshot-ove)
    private bool showUI = true;

    void Start()
    {
        SetupScene();
        SetupShadowCamera();
        SetupMaterials();
        ApplyTechnique(currentTechnique);
    }

    void Update()
    {
        // FPS
        fpsTimer += Time.unscaledDeltaTime;
        fpsCount++;
        if (fpsTimer >= 0.5f)
        {
            currentFPS = fpsCount / fpsTimer;
            fpsTimer = 0;
            fpsCount = 0;
        }

        // strelice levo/desno = rotiraj svetlo
        float lightInput = 0;
        if (Input.GetKey(KeyCode.LeftArrow)) lightInput -= 1;
        if (Input.GetKey(KeyCode.RightArrow)) lightInput += 1;
        if (lightInput != 0)
        {
            mainLight.transform.RotateAround(Vector3.zero, Vector3.up, lightInput * lightRotationSpeed * Time.deltaTime);
        }

        // strelice gore/dole = visina svetla
        float lightVertical = 0;
        if (Input.GetKey(KeyCode.UpArrow)) lightVertical += 1;
        if (Input.GetKey(KeyCode.DownArrow)) lightVertical -= 1;
        if (lightVertical != 0)
        {
            Vector3 euler = mainLight.transform.eulerAngles;
            euler.x = Mathf.Clamp(euler.x - lightVertical * lightRotationSpeed * Time.deltaTime, 10, 80);
            mainLight.transform.eulerAngles = euler;
        }

        if (Input.GetKeyDown(KeyCode.H)) showUI = !showUI;

        // tasteri 0-3 = shadow tehnika
        if (Input.GetKeyDown(KeyCode.Alpha0)) ApplyTechnique(0);
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyTechnique(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyTechnique(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyTechnique(3);

        // shadow mapa svaki frame pre renderovanja scene
        shadowMapCamera.RenderShadowMap(mainLight);
    }

    void OnGUI()
    {
        if (!showUI) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 14;
        boxStyle.alignment = TextAnchor.UpperLeft;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 13;

        GUIStyle activeStyle = new GUIStyle(GUI.skin.label);
        activeStyle.fontSize = 14;
        activeStyle.fontStyle = FontStyle.Bold;
        activeStyle.normal.textColor = Color.green;

        GUIStyle inactiveStyle = new GUIStyle(GUI.skin.label);
        inactiveStyle.fontSize = 13;
        inactiveStyle.normal.textColor = Color.gray;

        float x = 10, y = 10, w = 380, lineH = 22;

        GUI.Box(new Rect(x, y, w, 210), "  Shadow Techniques Demo", boxStyle);
        y += 28;

        for (int i = 0; i < techniqueNames.Length; i++)
        {
            string prefix = (i == currentTechnique) ? "► " : "  ";
            GUIStyle style = (i == currentTechnique) ? activeStyle : inactiveStyle;
            GUI.Label(new Rect(x + 10, y, w, lineH), prefix + i + " = " + techniqueNames[i], style);
            y += lineH;
        }

        y += 5;
        GUI.Label(new Rect(x + 10, y, w, lineH), "Strelice ←→ = rotiraj svetlo", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x + 10, y, w, lineH), "Strelice ↑↓ = visina svetla", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x + 10, y, w, lineH), "Desni klik + vuci = rotiraj kameru", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x + 10, y, w, lineH),

        string.Format("FPS: {0:F0}", currentFPS), labelStyle);
    }

    private void SetupScene()
    {
        // skybox
        Shader skyboxShader = Shader.Find("Skybox/Procedural");
        if (skyboxShader != null)
        {
            Material skyboxMat = new Material(skyboxShader);
            skyboxMat.SetFloat("_SunDisk", 2);
            skyboxMat.SetFloat("_SunSize", 0.04f);
            skyboxMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyboxMat.SetFloat("_Exposure", 1.3f);
            RenderSettings.skybox = skyboxMat;
        }

        // directional light BEZ Unity-jevih senki - implementiramo svoje u custom shaderima
        // https://docs.unity3d.com/ScriptReference/Light-shadows.html
        GameObject lightObj = new GameObject("Directional Light");
        mainLight = lightObj.AddComponent<Light>();
        mainLight.type = LightType.Directional;
        mainLight.color = new Color(1f, 0.96f, 0.84f);
        mainLight.intensity = 1.2f;
        mainLight.shadows = LightShadows.None;
        lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // podloga
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3, 1, 3);
        ground.transform.position = Vector3.zero;

        // 6 objekata za demonstraciju
        sceneObjects = new GameObject[7];
        sceneObjects[0] = ground;

        sceneObjects[1] = CreateObject(PrimitiveType.Cube, new Vector3(-4, 1, 0), Vector3.one * 2, "Cube1");
        sceneObjects[2] = CreateObject(PrimitiveType.Cube, new Vector3(0, 0.5f, 3), Vector3.one, "Cube2");
        sceneObjects[3] = CreateObject(PrimitiveType.Cube, new Vector3(3, 1.5f, -2), new Vector3(1, 3, 1), "TallCube");

        sceneObjects[4] = CreateObject(PrimitiveType.Sphere, new Vector3(2, 1, 2), Vector3.one * 2, "Sphere1");
        sceneObjects[5] = CreateObject(PrimitiveType.Sphere, new Vector3(-2, 0.5f, -3), Vector3.one, "Sphere2");

        sceneObjects[6] = CreateObject(PrimitiveType.Cylinder, new Vector3(5, 1, 0), new Vector3(1, 2, 1), "Cylinder");

        Camera.main.transform.position = new Vector3(0, 8, -12);
        Camera.main.transform.LookAt(Vector3.zero);
        Camera.main.gameObject.AddComponent<OrbitCamera>();
    }

    private GameObject CreateObject(PrimitiveType type, Vector3 pos, Vector3 scale, string name)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        return obj;
    }

    private void SetupShadowCamera()
    {
        GameObject shadowCamObj = new GameObject("ShadowMapCamera");
        shadowMapCamera = shadowCamObj.AddComponent<ShadowMapCamera>();

        // refleksija jer programski dodajemo komponentu (ne kroz Inspector), a polje je private
        // https://docs.microsoft.com/en-us/dotnet/api/system.reflection
        var field = typeof(ShadowMapCamera).GetField("shadowCasterShader",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null && shadowCasterShader != null)
        {
            field.SetValue(shadowMapCamera, shadowCasterShader);
        }
    }

    private void SetupMaterials()
    {
        techniqueNames = new string[]
        {
            "No Shadows (reference)",
            "Basic Shadow Mapping [Williams 1978]",
            "PCF - Percentage Closer Filtering [Reeves 1987]",
            "Variance Shadow Maps [Donnelly 2006]"
        };

        shadowMaterials = new Material[4];

        shadowMaterials[0] = new Material(noShadowShader);
        shadowMaterials[1] = new Material(basicShadowShader);
        shadowMaterials[2] = new Material(pcfShadowShader);
        shadowMaterials[3] = new Material(vsmShadowShader);

        Color[] colors = new Color[]
        {
            new Color(0.6f, 0.6f, 0.6f), // ground
            new Color(0.9f, 0.3f, 0.3f), // crvena
            new Color(0.3f, 0.9f, 0.3f), // zelena
            new Color(0.3f, 0.3f, 0.9f), // plava
            new Color(0.9f, 0.9f, 0.3f), // zuta
            new Color(0.9f, 0.5f, 0.2f), // narandzasta
            new Color(0.7f, 0.3f, 0.9f), // ljubicasta
        };

        for (int i = 0; i < sceneObjects.Length; i++)
        {
            sceneObjects[i].GetComponent<Renderer>().material.color = colors[i];
        }
    }

    private void ApplyTechnique(int index)
    {
        if (index < 0 || index >= shadowMaterials.Length) return;

        currentTechnique = index;

        Color[] colors = new Color[]
        {
            new Color(0.6f, 0.6f, 0.6f),
            new Color(0.9f, 0.3f, 0.3f),
            new Color(0.3f, 0.9f, 0.3f),
            new Color(0.3f, 0.3f, 0.9f),
            new Color(0.9f, 0.9f, 0.3f),
            new Color(0.9f, 0.5f, 0.2f),
            new Color(0.7f, 0.3f, 0.9f),
        };

        for (int i = 0; i < sceneObjects.Length; i++)
        {
            Material mat = new Material(shadowMaterials[currentTechnique]);
            mat.SetColor("_Color", colors[i]);
            sceneObjects[i].GetComponent<Renderer>().material = mat;
        }

        // VSM (index 3): Bilinear - momenti su filterabilni, hardver interpolira korektno
        // Basic/PCF (index 1, 2): Point - bilinearna interpolacija sirovih dubina daje pogresne rezultate
        shadowMapCamera.SetFilterMode(index == 3 ? FilterMode.Bilinear : FilterMode.Point);

        Debug.Log("Shadow technique: " + techniqueNames[currentTechnique]);
    }
}
