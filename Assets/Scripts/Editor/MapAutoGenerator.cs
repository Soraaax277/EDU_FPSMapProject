using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;
using System.Collections.Generic;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using System.Linq;

public class MapAutoGenerator : EditorWindow
{
    [MenuItem("FPS/FINAL ROUND MATCH BUILDER")]
    public static void ShowWindow()
    {
        GetWindow<MapAutoGenerator>("Full Match Builder");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        if (GUILayout.Button("BUILD ROUND MATCH (Full Sketch Layout)", GUILayout.Height(40))) BuildEverything();

        GUILayout.Space(20);
        if (GUILayout.Button("BAKE NAVMESH", GUILayout.Height(50))) BakeNavMeshOnly();
    }
    private static Texture2D GenerateWallTexture(int res = 512)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, true);
        tex.name = "WallTex_Brick";

        int brickRows    = 8;
        int brickCols    = 6;
        int mortarPx     = Mathf.Max(2, res / 80);
        int brickH       = (res / brickRows);
        int brickW       = (res / brickCols);

        Color mortarColor  = new Color(0.55f, 0.53f, 0.50f);
        Color brickBase    = new Color(0.32f, 0.30f, 0.28f);
        Color brickTint1   = new Color(0.38f, 0.34f, 0.30f);
        Color brickTint2   = new Color(0.28f, 0.26f, 0.24f);

        System.Random rng = new System.Random(42);

        Color[] pixels = new Color[res * res];

        for (int y = 0; y < res; y++)
        {
            int row = y / brickH;
            int localY = y % brickH;
            bool isMortarY = localY < mortarPx || localY >= brickH - mortarPx;

            int offsetX = (row % 2 == 0) ? 0 : brickW / 2;

            for (int x = 0; x < res; x++)
            {
                int shiftedX = (x + offsetX) % res;
                int col       = shiftedX / brickW;
                int localX    = shiftedX % brickW;
                bool isMortarX = localX < mortarPx || localX >= brickW - mortarPx;

                Color c;
                if (isMortarX || isMortarY)
                {
                    float n = (float)rng.NextDouble() * 0.04f - 0.02f;
                    c = new Color(mortarColor.r + n, mortarColor.g + n, mortarColor.b + n);
                }
                else
                {
                    float chipNoise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                    
                    float distToEdgeX = Mathf.Min(localX - mortarPx, (brickW - mortarPx) - localX);
                    float distToEdgeY = Mathf.Min(localY - mortarPx, (brickH - mortarPx) - localY);
                    float edgeDist = Mathf.Min(distToEdgeX, distToEdgeY);
                    
                    bool isChipped = chipNoise > 0.72f || (edgeDist < 3.0f && chipNoise > 0.45f);

                    if (isChipped)
                    {
                        float damageDepth = (chipNoise - 0.7f) * 0.4f;
                        c = new Color(0.18f - damageDepth, 0.17f - damageDepth, 0.16f - damageDepth);
                    }
                    else
                    {
                        float brickNoise = (float)rng.NextDouble() * 0.04f - 0.02f;
                        Color tint = ((col + row) % 3 == 0) ? brickTint1 : ((col + row) % 3 == 1) ? brickTint2 : brickBase;
                        c = new Color(
                            Mathf.Clamp01(tint.r + brickNoise),
                            Mathf.Clamp01(tint.g + brickNoise),
                            Mathf.Clamp01(tint.b + brickNoise));
                    }
                }
                pixels[y * res + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private static float FBM(float x, float y, int octaves)
    {
        float sum = 0f;
        float amp = 1f;
        float freq = 1f;
        float max = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            max += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / max;
    }

    private static T SaveAsset<T>(T asset, string folder, string name) where T : UnityEngine.Object
    {
        string basePath = "Assets/GeneratedMapAssets";
        if (!AssetDatabase.IsValidFolder(basePath)) AssetDatabase.CreateFolder("Assets", "GeneratedMapAssets");
            
        string folderPath = basePath + "/" + folder;
        if (!AssetDatabase.IsValidFolder(folderPath)) AssetDatabase.CreateFolder(basePath, folder);

        if (asset is Texture2D tex)
        {
            string pngPath = folderPath + "/" + name + ".png";
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            
            TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = name.Contains("Wall") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<T>(pngPath);
        }

        string fullPath = folderPath + "/" + name + ".asset";
        
        T existing = AssetDatabase.LoadAssetAtPath<T>(fullPath);
        if (existing != null)
        {
            if (asset is Mesh m && existing is Mesh em)
            {
                em.Clear(); em.vertices = m.vertices; em.uv = m.uv; em.triangles = m.triangles;
                em.RecalculateNormals(); em.RecalculateBounds();
                return existing as T;
            }
            if (asset is Material mat && existing is Material emat)
            {
                emat.CopyPropertiesFromMaterial(mat);
                return existing as T;
            }
            AssetDatabase.DeleteAsset(fullPath); 
        }

        AssetDatabase.CreateAsset(asset, fullPath);
        return asset;
    }

    private static Texture2D GenerateFloorTexture(int res, float craterSeedX, float craterSeedZ)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, true);
        tex.name = "FloorTex_PhotorealCobblestone_Unique";
        Color[] pixels = new Color[res * res];

        int tilesX = 160; 
        int tilePx = res / tilesX; 

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float wx = (x / (float)res) * 800f;
                float wy = (y / (float)res) * 600f;

                float craterNoise = Mathf.PerlinNoise(wx * 0.012f + craterSeedX, wy * 0.012f + craterSeedZ);

                float biomeNoise = FBM(wx * 0.005f + 10f, wy * 0.005f + 10f, 2); 
                
                float dirtFactor  = FBM(wx * 0.02f + 25f, wy * 0.02f + 25f, 2);
                float grassFactor = FBM(wx * 0.05f + 45f, wy * 0.05f + 45f, 2);

                dirtFactor += (biomeNoise - 0.5f) * 1.2f;
                grassFactor += (biomeNoise - 0.5f) * 1.2f;

                float lx = x % tilePx;
                float ly = y % tilePx;
                int tileRow = y / tilePx;
                if (tileRow % 2 == 1) lx = (x + tilePx / 2) % tilePx;

                float nx = lx / tilePx;
                float ny = ly / tilePx;

                float distSquare = Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(ny - 0.5f)) * 2f; 
                float distCircle = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f)) * 2f;
                float distFromCenter = Mathf.Lerp(distSquare, distCircle, 0.3f); 
                
                float tileProfile = Mathf.SmoothStep(0.70f, 1.0f, distFromCenter); 

                if (tileProfile > 0.5f) grassFactor += tileProfile * 0.6f;

                float craterBlend = 0f;
                if (craterNoise > 0.50f) craterBlend = Mathf.Clamp01((craterNoise - 0.50f) * 8f);

                bool isGrass = (grassFactor > 0.60f && dirtFactor > 0.40f);
                bool isMud = (dirtFactor > 0.50f) || (craterBlend > 0f);

                Color mudC = Color.black;
                Color grassC = Color.black;
                Color stoneC = Color.black;

                if (isMud || isGrass)
                {
                    float mudGrit = FBM(wx * 1f, wy * 1f, 3);
                    mudC = Color.Lerp(new Color(0.18f,0.14f,0.10f), new Color(0.35f,0.28f,0.22f), mudGrit);
                }

                if (isGrass)
                {
                    float grassGrit = FBM(wx * 2f, wy * 2f, 3);
                    float grassGritRight = FBM((wx + 0.1f) * 2f, wy * 2f, 3);
                    float bump = Mathf.Clamp01((grassGritRight - grassGrit) * 6f + 0.5f);
                    
                    grassC = Color.Lerp(new Color(0.08f,0.22f,0.05f), new Color(0.25f,0.45f,0.15f), grassGrit);
                    grassC *= Mathf.Lerp(0.5f, 1.3f, bump); 
                }

                if (!isGrass && craterBlend < 0.99f && dirtFactor < 0.65f)
                {
                    float stoneGrit = FBM(wx * 2f, wy * 2f, 3); 
                    float edgeDarken = 1f - (tileProfile * 0.8f); 
                    stoneC = Color.Lerp(new Color(0.25f,0.25f,0.28f), new Color(0.40f,0.42f,0.45f), stoneGrit) * edgeDarken;
                }

                Color c;
                if (isGrass)
                {
                    float gBlend = Mathf.SmoothStep(0.60f, 0.70f, grassFactor);
                    c = Color.Lerp(isMud ? mudC : stoneC, grassC, gBlend);
                }
                else if (isMud)
                {
                    float dBlend = Mathf.SmoothStep(0.50f, 0.65f, dirtFactor);
                    c = Color.Lerp(stoneC, mudC, dBlend);
                }
                else
                {
                    c = stoneC;
                }

                if (craterBlend > 0f) c = Color.Lerp(c, mudC, craterBlend);

                pixels[y * res + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp; 
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private static void ApplyTexture(Material mat, Texture2D tex, Vector2 tiling)
    {
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        else if (mat.HasProperty("_MainTex"))
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;
        }
    }

    private void BuildEverything()
    {
        GameObject root = GameObject.Find("Map_TheConduit");
        if (root != null) DestroyImmediate(root);
        root = new GameObject("Map_TheConduit");

        if (AssetDatabase.IsValidFolder("Assets/GeneratedMapAssets"))
            AssetDatabase.DeleteAsset("Assets/GeneratedMapAssets");
        AssetDatabase.CreateFolder("Assets", "GeneratedMapAssets");
        AssetDatabase.CreateFolder("Assets/GeneratedMapAssets", "Textures");
        AssetDatabase.CreateFolder("Assets/GeneratedMapAssets", "Materials");
        AssetDatabase.CreateFolder("Assets/GeneratedMapAssets", "Meshes");

        root.AddComponent<RoundManager>();
        NavMeshSurface nav = root.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;

        float craterSeedX = Random.value * 100f;
        float craterSeedZ = Random.value * 100f;

        Texture2D wallTex  = SaveAsset(GenerateWallTexture(512), "Textures", "WallTex");
        Texture2D floorTex = SaveAsset(GenerateFloorTexture(2048, craterSeedX, craterSeedZ), "Textures", "FloorTex");

        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material wallM  = new Material(s);
        wallM.color     = new Color(0.85f, 0.85f, 0.88f);  
        ApplyTexture(wallM, wallTex, new Vector2(3f, 2f)); 
        wallM = SaveAsset(wallM, "Materials", "WallMaterial");

        Material floorM = new Material(s);
        floorM.color    = Color.white;
        ApplyTexture(floorM, floorTex, new Vector2(1f, 1f)); 
        floorM = SaveAsset(floorM, "Materials", "FloorMaterial");


        SetupAtmosphere(root.transform);

        GameObject wallsP = new GameObject("Walls"); wallsP.transform.SetParent(root.transform);
        float rX = 280f, rZ = 160f; int segs = 64;
        for (int i = 0; i < segs; i++)
        {
            float t  = (i       * 2.0f * Mathf.PI) / segs;
            float nt = ((i + 1) * 2.0f * Mathf.PI) / segs;
            Vector3 p1 = new Vector3(rX * Mathf.Cos(t),  0, rZ * Mathf.Sin(t));
            Vector3 p2 = new Vector3(rX * Mathf.Cos(nt), 0, rZ * Mathf.Sin(nt));
            Vector3 p  = (p1 + p2) / 2f;
            float dist = Vector3.Distance(p1, p2);
            float rot  = Mathf.Atan2(p2.x - p1.x, p2.z - p1.z) * Mathf.Rad2Deg;
            CreateWall(new WallData(p.x, p.z, 2f, dist, rot, 15f), wallsP.transform, "Border_", wallM);
        }

        List<WallData> sw = new List<WallData>();

        sw.Add(new WallData(0,    150, 60, 10, 0, 10f));
        sw.Add(new WallData(0,    120, 120, 5, 0));

        sw.Add(new WallData(-80,  70,  80, 5, 0)); sw.Add(new WallData(-80,  95,  5, 50, 0));
        sw.Add(new WallData( 80,  70,  80, 5, 0)); sw.Add(new WallData( 80,  95,  5, 50, 0));
        sw.Add(new WallData(-80, -70,  80, 5, 0)); sw.Add(new WallData(-80, -95,  5, 50, 0));
        sw.Add(new WallData( 80, -70,  80, 5, 0)); sw.Add(new WallData( 80, -95,  5, 50, 0));

        sw.Add(new WallData(-210, 40,  5, 80, 0)); sw.Add(new WallData(-180,  80, 60, 5, 0));
        sw.Add(new WallData( 210, 40,  5, 80, 0)); sw.Add(new WallData( 180,  80, 60, 5, 0));

        for (int x = -160; x <= 160; x += 80)
        {
            sw.Add(new WallData(x,      -125, 5, 70, 0));
            sw.Add(new WallData(x - 40, -100, 40, 5, 0));
        }
        sw.Add(new WallData(0, -160, 480, 5, 0));

        foreach (var w in sw) CreateWall(w, wallsP.transform, "S_", wallM);

        GameObject gr = CreateCrateredGround(root.transform, floorM, 800f, 600f, 160, 120, craterSeedX, craterSeedZ);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "The_Core"; core.transform.SetParent(root.transform);
        core.transform.localPosition  = new Vector3(0, 0.1f, 0);
        core.transform.localScale     = new Vector3(18, 0.1f, 18);
        core.GetComponent<Collider>().isTrigger = true;
        CapturePoint cp = core.AddComponent<CapturePoint>();

        GameObject hud = new GameObject("CapturePoint_HUD");
        hud.transform.SetParent(root.transform);
        CapturePointUI cpUI = hud.AddComponent<CapturePointUI>();
        cpUI.capturePoint = cp;

        GameObject bul = GenerateBullet(root.transform, s);
        string[] assets = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        InputActionAsset inputs = (assets.Length > 0)
            ? AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(assets[0]))
            : null;

        GameObject specGO = new GameObject("SpectatorController");
        specGO.transform.SetParent(root.transform);
        specGO.AddComponent<SpectatorController>();


        CreateFullChar("Player",     new Vector3(-250,1,  0), "Green", true,  root.transform, bul, s, inputs);

        CreateFullChar("Teammate_1", new Vector3(-260,1, 30), "Green", false, root.transform, bul, s, null);
        CreateFullChar("Teammate_2", new Vector3(-260,1,-30), "Green", false, root.transform, bul, s, null);
        CreateFullChar("Teammate_3", new Vector3(-240,1, 55), "Green", false, root.transform, bul, s, null);
        CreateFullChar("Teammate_4", new Vector3(-240,1,-55), "Green", false, root.transform, bul, s, null);

        CreateFullChar("Enemy_1",    new Vector3( 250,1,  0), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_2",    new Vector3( 260,1, 30), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_3",    new Vector3( 260,1,-30), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_4",    new Vector3( 240,1, 55), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_5",    new Vector3( 240,1,-55), "Red",   false, root.transform, bul, s, null);

        nav.BuildNavMesh();
        if (nav.navMeshData != null) nav.navMeshData = SaveAsset(nav.navMeshData, "Meshes", "Baked_NavMeshData");

        GameObject overviewGO = new GameObject("Overview_Camera");
        overviewGO.transform.SetParent(root.transform);
        overviewGO.transform.position = new Vector3(0, 150f, -80f);
        overviewGO.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        Camera overviewCam = overviewGO.AddComponent<Camera>();
        overviewCam.fieldOfView  = 75f;
        overviewCam.farClipPlane = 1000f;
        overviewCam.enabled      = false;

        Debug.Log("Build Complete! 5v5, textured walls/floor, spectator system active.");
    }

    private void BakeNavMeshOnly()
    {
        GameObject root = GameObject.Find("Map_TheConduit");
        if (root != null) { root.GetComponent<NavMeshSurface>().BuildNavMesh(); Debug.Log("BAKE OK!"); }
    }

    private Camera CreateFullChar(string n, Vector3 p, string t, bool isP,
                                  Transform pr, GameObject b, Shader s,
                                  InputActionAsset i)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        c.name = n; c.transform.SetParent(pr); c.transform.localPosition = p;

        Rigidbody rb = c.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        Material m = new Material(s);
        m.color = t == "Green" ? Color.green : Color.red;
        c.GetComponent<Renderer>().sharedMaterial = SaveAsset(m, "Materials", $"CharMat_{t}_{n}");

        GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.transform.SetParent(c.transform);
        gun.transform.localPosition = new Vector3(0.5f, 0.4f, 0.7f);
        gun.transform.localScale    = new Vector3(0.3f, 0.3f, 1.2f);
        Material gm = new Material(s); gm.color = Color.black;
        gun.GetComponent<Renderer>().sharedMaterial = SaveAsset(gm, "Materials", $"GunMat_{t}_{n}");

        Collider gunCol = gun.GetComponent<Collider>();
        if (gunCol != null) Object.DestroyImmediate(gunCol);

        GameObject sp = new GameObject("SP");
        sp.transform.SetParent(gun.transform);
        sp.transform.localPosition = new Vector3(0, 0, 0.6f);

        Health h = c.AddComponent<Health>(); h.team = t;
        BaseCharacter bc = c.AddComponent<BaseCharacter>(); bc.team = t;

        Camera returnCam = null;

        if (isP)
        {
            c.tag = "Player"; c.AddComponent<CharacterController>();
            PlayerCharacter pc = c.AddComponent<PlayerCharacter>();
            pc.team = t; pc.bulletPrefab = b; pc.shootPoint = sp.transform;
            pc.gunTransform = gun.transform;

            Camera main = Camera.main;
            if (main == null)
            {
                GameObject camGO = new GameObject("MainCamera");
                main = camGO.AddComponent<Camera>();
            }
            main.tag = "MainCamera";
            main.nearClipPlane = 0.05f;  
            main.transform.SetParent(pr);
            main.transform.position = p + new Vector3(0, 0.8f, 0.2f);

            SimpleCameraFollow scf = main.gameObject.GetComponent<SimpleCameraFollow>();
            if (scf == null) scf = main.gameObject.AddComponent<SimpleCameraFollow>();
            scf.target         = c.transform;
            scf.offset         = new Vector3(0, 0.8f, 0.2f);
            scf.useWorldOffset = false;  
            scf.lookAtTarget   = false;

            if (i != null) { PlayerInput pi = c.AddComponent<PlayerInput>(); pi.actions = i; pi.defaultActionMap = "Player"; }

            returnCam = main;
        }
        else
        {
            c.tag = "AI"; c.AddComponent<UnityEngine.AI.NavMeshAgent>();
            CombatAI ai = c.AddComponent<CombatAI>();
            ai.team = t; ai.bulletPrefab = b; ai.shootPoint = sp.transform;

            if (t == "Green")
            {
                GameObject camGO = new GameObject($"SpectatorCam_{n}");
                camGO.transform.SetParent(pr);

                camGO.transform.position = p + new Vector3(0, 15f, 0);

                Camera cam = camGO.AddComponent<Camera>();
                cam.fieldOfView  = 65f;
                cam.farClipPlane = 600f;
                cam.enabled      = false;  

                SimpleCameraFollow follow = camGO.AddComponent<SimpleCameraFollow>();
                follow.target         = c.transform;
                follow.offset         = new Vector3(0, 15f, 0);
                follow.useWorldOffset  = true;
                follow.lookAtTarget    = true;  
            }
        }
        return returnCam;
    }

    private GameObject GenerateBullet(Transform r, Shader s)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Bullet_Prefab"; b.transform.localScale = Vector3.one * 0.3f;
        Material m = new Material(s); m.color = Color.yellow;
        b.GetComponent<Renderer>().sharedMaterial = SaveAsset(m, "Materials", "BulletMat");
        b.GetComponent<Collider>().isTrigger = true;
        b.AddComponent<Bullet>();
        b.transform.SetParent(r);
        b.transform.localPosition = Vector3.down * 100;
        return b;
    }

    private static void SetupAtmosphere(Transform root)
    {
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material sky = new Material(skyShader);
            sky.SetFloat("_SunSize",            0.03f);   
            sky.SetFloat("_SunSizeConvergence", 8f);
            sky.SetFloat("_AtmosphereThickness",0.55f);   
            sky.SetColor("_SkyTint",   new Color(0.04f, 0.06f, 0.14f));  
            sky.SetColor("_GroundColor",new Color(0.09f, 0.08f, 0.07f)); 
            sky.SetFloat("_Exposure",  0.65f);
            RenderSettings.skybox = SaveAsset(sky, "Materials", "Skybox_Procedural");
            DynamicGI.UpdateEnvironment();
        }

        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.Exponential;
        RenderSettings.fogColor   = new Color(0.04f, 0.06f, 0.12f);
        RenderSettings.fogDensity = 0.006f;

        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.14f); 

        MakeLight(root, "Moon", LightType.Directional,
                  new Vector3(35f, -120f, 0f), Vector3.zero,
                  new Color(0.55f, 0.65f, 0.90f), 
                  intensity: 0.35f, range: 0f);

        var sconces = new (float x, float z)[] {
            (-220,   0), ( 220,   0),
            (-180, 110), ( 180, 110),
            (-180,-110), ( 180,-110),
            (   0, 145), (   0,-145),
            (-110,  90), ( 110,  90),
            (-110, -90), ( 110, -90),
        };
        int sci = 0;
        foreach (var sc in sconces)
        {
            MakeLight(root, $"Sconce_{sci++}", LightType.Point,
                      Vector3.zero, new Vector3(sc.x, 9f, sc.z),
                      new Color(1.0f, 0.62f, 0.18f),  
                      intensity: 2.8f, range: 55f);
        }

        var fills = new (float x, float z)[] {
            (-80, 82), ( 80, 82), (-80,-82), ( 80,-82),
            (-200, 40), (200, 40),
        };
        int fi = 0;
        foreach (var f in fills)
        {
            MakeLight(root, $"Fill_{fi++}", LightType.Point,
                      Vector3.zero, new Vector3(f.x, 11f, f.z),
                      new Color(0.40f, 0.55f, 0.80f),  
                      intensity: 1.6f, range: 60f);
        }

        MakeLight(root, "Spot_NorthPerch", LightType.Spot,
                  new Vector3(80f, 0f, 0f), new Vector3(0f, 20f, 148f),
                  new Color(0.9f, 0.95f, 1.0f), intensity: 3.5f, range: 80f, spotAngle: 35f);

        MakeLight(root, "Spot_SouthCQB_L", LightType.Spot,
                  new Vector3(80f, 0f, 0f), new Vector3(-120f, 18f, -120f),
                  new Color(1.0f, 0.80f, 0.50f), intensity: 3.0f, range: 70f, spotAngle: 40f);

        MakeLight(root, "Spot_SouthCQB_R", LightType.Spot,
                  new Vector3(80f, 0f, 0f), new Vector3( 120f, 18f, -120f),
                  new Color(1.0f, 0.80f, 0.50f), intensity: 3.0f, range: 70f, spotAngle: 40f);

        var coreAccents = new Vector3[] {
            new Vector3( 12f, 2f,   0f), new Vector3(-12f, 2f,  0f),
            new Vector3(  0f, 2f,  12f), new Vector3(  0f, 2f,-12f),
        };
        int ci = 0;
        foreach (var ca in coreAccents)
        {
            MakeLight(root, $"Core_{ci++}", LightType.Point, Vector3.zero, ca,
                      new Color(0.45f, 0.20f, 1.0f),  
                      intensity: 2.2f, range: 28f);
        }
        MakeLight(root, "Core_Centre", LightType.Point,
                  Vector3.zero, new Vector3(0f, 0.5f, 0f),
                  new Color(0.30f, 0.15f, 0.90f),
                  intensity: 3.5f, range: 22f);
    }

    private static GameObject MakeLight(Transform parent, string name,
        LightType type, Vector3 eulerAngles, Vector3 position,
        Color color, float intensity, float range, float spotAngle = 60f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = eulerAngles;

        Light l = go.AddComponent<Light>();
        l.type      = type;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.spotAngle = spotAngle;
        l.shadows   = LightShadows.Soft;
        return go;
    }

    private GameObject CreateCrateredGround(Transform parent, Material mat, float width, float length, int segmentsX, int segmentsZ, float craterSeedX, float craterSeedZ)
    {
        GameObject go = new GameObject("Ground_Cratered");
        go.transform.SetParent(parent);
        
        Mesh mesh = new Mesh();
        mesh.name = "GroundGrid";

        Vector3[] vertices  = new Vector3[(segmentsX + 1) * (segmentsZ + 1)];
        Vector2[] uvs       = new Vector2[vertices.Length];
        int[] triangles     = new int[segmentsX * segmentsZ * 6];

        float halfW = width / 2f;
        float halfL = length / 2f;
        float dx = width / segmentsX;
        float dz = length / segmentsZ;

        int vi = 0;
        int ti = 0;

        for (int z = 0; z <= segmentsZ; z++)
        {
            for (int x = 0; x <= segmentsX; x++)
            {
                float px = -halfW + x * dx;
                float pz = -halfL + z * dz;
                
                float y = 0f;
                float craterNoise = Mathf.PerlinNoise(px * 0.012f + craterSeedX, pz * 0.012f + craterSeedZ);
                
                if (craterNoise > 0.55f)
                {
                    float normalizedDepth = (craterNoise - 0.55f) / 0.45f;
                    float curve = normalizedDepth * normalizedDepth * (3f - 2f * normalizedDepth); 
                    y = -curve * 6.5f; 
                    if (y < -2f) y += (Mathf.PerlinNoise(px * 0.3f, pz * 0.3f) - 0.5f) * 0.8f;
                }
                else
                {
                    y = (Mathf.PerlinNoise(px * 0.15f, pz * 0.15f) - 0.5f) * 0.4f;
                }

                float distFromCenter = Mathf.Max(Mathf.Abs(px), Mathf.Abs(pz));
                if (distFromCenter < 50f)
                {
                    float blend = Mathf.Clamp01((distFromCenter - 25f) / 25f);
                    y = Mathf.Lerp(0f, y, blend); 
                }

                if (pz > 120f || pz < -120f)
                {
                    float distFromSpawnEdge = Mathf.Abs(Mathf.Abs(pz) - 120f);
                    float blend = Mathf.Clamp01(distFromSpawnEdge / 20f);
                    y = Mathf.Lerp(y, 0f, blend); 
                }

                vertices[vi] = new Vector3(px, y, pz);
                
                uvs[vi] = new Vector2((px + halfW) / width, (pz + halfL) / length);

                if (x < segmentsX && z < segmentsZ)
                {
                    triangles[ti]     = vi;
                    triangles[ti + 1] = vi + segmentsX + 1;
                    triangles[ti + 2] = vi + 1;

                    triangles[ti + 3] = vi + 1;
                    triangles[ti + 4] = vi + segmentsX + 1;
                    triangles[ti + 5] = vi + segmentsX + 2;
                    ti += 6;
                }
                vi++;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh = SaveAsset(mesh, "Meshes", "GroundCrateredMesh");

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat; 

        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);

        return go;
    }

    private void CreateWall(WallData d, Transform p, string n, Material mat)
    {
        bool shouldChip = (Random.value < 0.35f);

        if (d.l < 8f) shouldChip = false;

        if (shouldChip)
        {
            GameObject go = new GameObject(n + "Wall_Destroyed");
            go.transform.SetParent(p);
            go.transform.localPosition = new Vector3(d.x, d.h / 2, d.z);
            go.transform.rotation      = Quaternion.Euler(0, d.rot - 90f, 0);
            go.transform.localScale    = new Vector3(d.l, d.h, d.w);

            ProBuilderMesh mesh = go.AddComponent<ProBuilderMesh>();

            List<Vector3> pts = new List<Vector3>();
            pts.Add(new Vector3(-0.5f, -0.5f, 0f)); 
            pts.Add(new Vector3( 0.5f, -0.5f, 0f)); 

            int steps = Mathf.Max(15, Mathf.CeilToInt(d.l * 1.5f));
            float craterRadius = Mathf.Min(Random.Range(5f, 30f) / d.l, 0.45f);
            float craterDepth  = Mathf.Clamp(Random.Range(4f, 10f) / d.h, 0.2f, 0.85f);
            float noiseSeed    = Random.value * 100f;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = Mathf.Lerp(0.5f, -0.5f, t);
                
                float dist = Mathf.Abs(x);
                float drop = 0f;
                if (dist < craterRadius)
                {
                    float nDist = dist / craterRadius;
                    drop = (1f - nDist * nDist) * craterDepth;
                }
                
                float noise = (Mathf.PerlinNoise(x * 12f, noiseSeed) - 0.5f) * 0.25f;
                float y = 0.5f - drop + noise;
                
                float taper = Mathf.SmoothStep(0f, 1f, (0.5f - dist) * 10f);
                y = Mathf.Lerp(0.5f, y, taper);
                y = Mathf.Clamp(y, -0.45f, 0.5f);

                pts.Add(new Vector3(x, y, 0f));
            }

            mesh.CreateShapeFromPolygon(pts, 1f, false);
            mesh.ToMesh();
            mesh.Refresh();

            var positions = mesh.positions.ToList();
            float minZ = positions.Min(pos => pos.z);
            float maxZ = positions.Max(pos => pos.z);
            float centerZ = (minZ + maxZ) / 2f;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 v = positions[i];
                v.z -= centerZ; 

                if (v.y > -0.49f && v.y < 0.49f) 
                {
                    float joltZ = (Mathf.PerlinNoise(v.x * 25f, v.y * 25f) - 0.5f) * 0.4f;
                    v.z += joltZ * (1.5f / d.w); 
                }
                positions[i] = v;
            }

            mesh.positions = positions;
            mesh.ToMesh();
            mesh.Refresh();

            // Strip ProBuilder and Serialize Mesh to disk!
            MeshFilter mf = go.GetComponent<MeshFilter>();
            Mesh cleanMesh = SaveAsset(mf.sharedMesh, "Meshes", "Wall_" + System.Guid.NewGuid().ToString().Substring(0, 8));
            mf.sharedMesh = cleanMesh;
            Object.DestroyImmediate(mesh);

            go.GetComponent<Renderer>().sharedMaterial = mat;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = cleanMesh;
        }
        else
        {
            GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = n + "Wall"; w.transform.SetParent(p);
            w.transform.localPosition = new Vector3(d.x, d.h / 2, d.z);
            w.transform.rotation      = Quaternion.Euler(0, d.rot, 0);
            w.transform.localScale    = new Vector3(d.w, d.h, d.l);
            w.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    private struct WallData
    {
        public float x, z, w, l, rot, h;
        public WallData(float x, float z, float w, float l, float r, float h = 12f)
        { this.x = x; this.z = z; this.w = w; this.l = l; this.rot = r; this.h = h; }
    }
}