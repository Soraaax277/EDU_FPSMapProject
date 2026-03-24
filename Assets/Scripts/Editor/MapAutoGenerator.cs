using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;
using System.Collections.Generic;

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

    // ─────────────────────────────────────────────────────────────
    //  TEXTURE GENERATION
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a procedural concrete-brick wall texture.
    /// Rows of bricks with mortar joints, subtle noise for a worn look.
    /// </summary>
    private static Texture2D GenerateWallTexture(int res = 512)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, true);
        tex.name = "WallTex_Brick";

        // Brick parameters
        int brickRows    = 8;
        int brickCols    = 6;
        int mortarPx     = Mathf.Max(2, res / 80);
        int brickH       = (res / brickRows);
        int brickW       = (res / brickCols);

        // Base palette
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

            // Offset every other row (running bond pattern)
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
                    // Mortar — slight noise
                    float n = (float)rng.NextDouble() * 0.04f - 0.02f;
                    c = new Color(mortarColor.r + n, mortarColor.g + n, mortarColor.b + n);
                }
                else
                {
                    // Brick — pick a tint per brick cell + per-pixel noise
                    float brickNoise = (float)rng.NextDouble() * 0.06f - 0.03f;
                    Color tint = ((col + row) % 3 == 0) ? brickTint1 : ((col + row) % 3 == 1) ? brickTint2 : brickBase;
                    c = new Color(
                        Mathf.Clamp01(tint.r + brickNoise),
                        Mathf.Clamp01(tint.g + brickNoise),
                        Mathf.Clamp01(tint.b + brickNoise));
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

    /// <summary>
    /// Generates a procedural tiled floor texture.
    /// Square tiles with thin grouting lines and subtle vignette per tile.
    /// </summary>
    private static Texture2D GenerateFloorTexture(int res = 512)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, true);
        tex.name = "FloorTex_Tile";

        int tileCount = 4;  // tiles per texture repeat
        int tilePx    = res / tileCount;
        int groutPx   = Mathf.Max(2, res / 128);

        Color groutColor = new Color(0.18f, 0.18f, 0.18f);
        Color tileBase   = new Color(0.22f, 0.23f, 0.25f);
        Color tileLight  = new Color(0.26f, 0.27f, 0.30f);

        System.Random rng = new System.Random(7);
        Color[] pixels = new Color[res * res];

        for (int y = 0; y < res; y++)
        {
            int tileRow = y / tilePx;
            int localY  = y % tilePx;
            bool isGroutY = localY < groutPx || localY >= tilePx - groutPx;

            for (int x = 0; x < res; x++)
            {
                int tileCol = x / tilePx;
                int localX  = x % tilePx;
                bool isGroutX = localX < groutPx || localX >= tilePx - groutPx;

                Color c;
                if (isGroutX || isGroutY)
                {
                    float n = (float)rng.NextDouble() * 0.02f;
                    c = new Color(groutColor.r + n, groutColor.g + n, groutColor.b + n);
                }
                else
                {
                    // Subtle inner vignette per tile
                    float uvX = (localX - groutPx) / (float)(tilePx - groutPx * 2);
                    float uvY = (localY - groutPx) / (float)(tilePx - groutPx * 2);
                    float edgeFactor = Mathf.Min(uvX, 1f - uvX) * Mathf.Min(uvY, 1f - uvY) * 8f;
                    edgeFactor = Mathf.Clamp01(edgeFactor);

                    // Alternate tile brightness in a checkerboard-like way for interest
                    Color baseC = ((tileRow + tileCol) % 2 == 0) ? tileBase : tileLight;
                    float noise = (float)rng.NextDouble() * 0.03f - 0.015f;
                    float bright = edgeFactor * 0.12f + noise;
                    c = new Color(
                        Mathf.Clamp01(baseC.r + bright),
                        Mathf.Clamp01(baseC.g + bright),
                        Mathf.Clamp01(baseC.b + bright));
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

    /// <summary>
    /// Applies a texture to a material, setting tiling so it looks correctly scaled.
    /// </summary>
    private static void ApplyTexture(Material mat, Texture2D tex, Vector2 tiling)
    {
        // Works for both URP/Lit (_BaseMap) and Standard (_MainTex)
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

    // ─────────────────────────────────────────────────────────────
    //  MAIN BUILD
    // ─────────────────────────────────────────────────────────────

    private void BuildEverything()
    {
        // 1. CLEAR PREVIOUS
        GameObject root = GameObject.Find("Map_TheConduit");
        if (root != null) DestroyImmediate(root);
        root = new GameObject("Map_TheConduit");

        // --- Manager & Nav ---
        root.AddComponent<RoundManager>();
        NavMeshSurface nav = root.AddComponent<NavMeshSurface>();
        nav.collectObjects = CollectObjects.Children;

        // --- Textures ---
        Texture2D wallTex  = GenerateWallTexture(512);
        Texture2D floorTex = GenerateFloorTexture(512);

        // --- Materials ---
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material wallM  = new Material(s);
        wallM.color     = new Color(0.85f, 0.85f, 0.88f);  // slight tint multiplied over texture
        ApplyTexture(wallM, wallTex, new Vector2(3f, 2f));  // 3 repeats wide, 2 tall per wall segment

        Material floorM = new Material(s);
        floorM.color    = new Color(0.90f, 0.90f, 0.92f);
        ApplyTexture(floorM, floorTex, new Vector2(80f, 60f)); // matches plane scale 80×60 → 1 unit = 1 tile

        // --- Lighting ---
        GameObject sun = new GameObject("Sun"); sun.transform.SetParent(root.transform);
        Light sunL = sun.AddComponent<Light>();
        sunL.type      = LightType.Directional;
        sunL.intensity = 1.2f;
        sunL.color     = new Color(1f, 0.97f, 0.90f);  // warm sunlight
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Ambient fill — slightly cool to contrast warm sun
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.25f);

        // --- 1. RUGBY OVAL BORDER ---
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

        // --- 2. FULL INTERNAL WALLS ---
        List<WallData> sw = new List<WallData>();

        // North Lane (Sniper)
        sw.Add(new WallData(0,    150, 60, 10, 0, 10f));
        sw.Add(new WallData(0,    120, 120, 5, 0));

        // Center Lane (Gateways & T-junctions)
        sw.Add(new WallData(-80,  70,  80, 5, 0)); sw.Add(new WallData(-80,  95,  5, 50, 0));
        sw.Add(new WallData( 80,  70,  80, 5, 0)); sw.Add(new WallData( 80,  95,  5, 50, 0));
        sw.Add(new WallData(-80, -70,  80, 5, 0)); sw.Add(new WallData(-80, -95,  5, 50, 0));
        sw.Add(new WallData( 80, -70,  80, 5, 0)); sw.Add(new WallData( 80, -95,  5, 50, 0));

        // Gateways (Near Spawns)
        sw.Add(new WallData(-210, 40,  5, 80, 0)); sw.Add(new WallData(-180,  80, 60, 5, 0));
        sw.Add(new WallData( 210, 40,  5, 80, 0)); sw.Add(new WallData( 180,  80, 60, 5, 0));

        // South Lane (CQB rooms)
        for (int x = -160; x <= 160; x += 80)
        {
            sw.Add(new WallData(x,      -125, 5, 70, 0));
            sw.Add(new WallData(x - 40, -100, 40, 5, 0));
        }
        sw.Add(new WallData(0, -160, 480, 5, 0));

        foreach (var w in sw) CreateWall(w, wallsP.transform, "S_", wallM);

        // --- 3. GROUND & CORE ---
        GameObject gr = GameObject.CreatePrimitive(PrimitiveType.Plane);
        gr.transform.SetParent(root.transform);
        gr.transform.localScale = new Vector3(80, 1, 60);
        gr.GetComponent<Renderer>().sharedMaterial = floorM;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "The_Core"; core.transform.SetParent(root.transform);
        core.transform.localPosition  = new Vector3(0, 0.1f, 0);
        core.transform.localScale     = new Vector3(18, 0.1f, 18);
        core.GetComponent<Collider>().isTrigger = true;
        CapturePoint cp = core.AddComponent<CapturePoint>();

        // --- HUD: Capture Point UI ---
        GameObject hud = new GameObject("CapturePoint_HUD");
        hud.transform.SetParent(root.transform);
        CapturePointUI cpUI = hud.AddComponent<CapturePointUI>();
        cpUI.capturePoint = cp;

        // --- 4. COMBAT & CAMERAS ---
        GameObject bul = GenerateBullet(root.transform, s);
        string[] assets = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        InputActionAsset inputs = (assets.Length > 0)
            ? AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(assets[0]))
            : null;

        // --- SpectatorController (self-discovers cameras in Start()) ---
        GameObject specGO = new GameObject("SpectatorController");
        specGO.transform.SetParent(root.transform);
        specGO.AddComponent<SpectatorController>();

        // 5v5 SPAWNS  (Green = left side, Red = right side)
        // Player
        CreateFullChar("Player",     new Vector3(-250,1,  0), "Green", true,  root.transform, bul, s, inputs);

        // Teammates
        CreateFullChar("Teammate_1", new Vector3(-260,1, 30), "Green", false, root.transform, bul, s, null);
        CreateFullChar("Teammate_2", new Vector3(-260,1,-30), "Green", false, root.transform, bul, s, null);
        CreateFullChar("Teammate_3", new Vector3(-240,1, 55), "Green", false, root.transform, bul, s, null);
        CreateFullChar("Teammate_4", new Vector3(-240,1,-55), "Green", false, root.transform, bul, s, null);

        // Enemies
        CreateFullChar("Enemy_1",    new Vector3( 250,1,  0), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_2",    new Vector3( 260,1, 30), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_3",    new Vector3( 260,1,-30), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_4",    new Vector3( 240,1, 55), "Red",   false, root.transform, bul, s, null);
        CreateFullChar("Enemy_5",    new Vector3( 240,1,-55), "Red",   false, root.transform, bul, s, null);

        nav.BuildNavMesh();

        // --- 5. PERSISTENT OVERVIEW CAMERA ---
        GameObject overviewGO = new GameObject("Overview_Camera");
        overviewGO.transform.SetParent(root.transform);
        overviewGO.transform.position = new Vector3(0, 150f, -80f);
        overviewGO.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        Camera overviewCam = overviewGO.AddComponent<Camera>();
        overviewCam.fieldOfView  = 75f;
        overviewCam.farClipPlane = 1000f;
        overviewCam.enabled      = false;
        // SpectatorController.Start() discovers Overview_Camera by name automatically

        Debug.Log("Build Complete! 5v5, textured walls/floor, spectator system active.");
    }

    private void BakeNavMeshOnly()
    {
        GameObject root = GameObject.Find("Map_TheConduit");
        if (root != null) { root.GetComponent<NavMeshSurface>().BuildNavMesh(); Debug.Log("BAKE OK!"); }
    }

    // Returns the player Camera if isP=true (unused externally now, kept for clarity).
    private Camera CreateFullChar(string n, Vector3 p, string t, bool isP,
                                  Transform pr, GameObject b, Shader s,
                                  InputActionAsset i)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        c.name = n; c.transform.SetParent(pr); c.transform.localPosition = p;

        // Rigidbody required for OnTriggerEnter to fire
        Rigidbody rb = c.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        Material m = new Material(s);
        m.color = t == "Green" ? Color.green : Color.red;
        c.GetComponent<Renderer>().sharedMaterial = m;

        GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.transform.SetParent(c.transform);
        gun.transform.localPosition = new Vector3(0.5f, 0.4f, 0.7f);
        gun.transform.localScale    = new Vector3(0.3f, 0.3f, 1.2f);
        Material gm = new Material(s); gm.color = Color.black;
        gun.GetComponent<Renderer>().sharedMaterial = gm;

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

            // Main FPS camera — detached from player so it survives death
            Camera main = Camera.main;
            if (main == null)
            {
                GameObject camGO = new GameObject("MainCamera");
                main = camGO.AddComponent<Camera>();
            }
            main.tag = "MainCamera";
            main.transform.SetParent(pr);
            main.transform.position = p + new Vector3(0, 0.8f, 0.2f);

            SimpleCameraFollow scf = main.gameObject.GetComponent<SimpleCameraFollow>();
            if (scf == null) scf = main.gameObject.AddComponent<SimpleCameraFollow>();
            scf.target         = c.transform;
            scf.offset         = new Vector3(0, 0.8f, 0.2f);
            scf.useWorldOffset = false;  // local-space: rotates with player (FPS)
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
                // Overhead spectator camera — named SpectatorCam_* so
                // SpectatorController.Start() can self-discover it.
                GameObject camGO = new GameObject($"SpectatorCam_{n}");
                camGO.transform.SetParent(pr);

                // Position directly above the teammate's spawn point at build time
                camGO.transform.position = p + new Vector3(0, 15f, 0);

                Camera cam = camGO.AddComponent<Camera>();
                cam.fieldOfView  = 65f;
                cam.farClipPlane = 600f;
                cam.enabled      = false;  // SpectatorController enables as needed

                SimpleCameraFollow follow = camGO.AddComponent<SimpleCameraFollow>();
                follow.target         = c.transform;
                follow.offset         = new Vector3(0, 15f, 0); // world-space: always straight above
                follow.useWorldOffset  = true;
                follow.lookAtTarget    = true;  // always faces down at the teammate
            }
        }
        return returnCam;
    }

    private GameObject GenerateBullet(Transform r, Shader s)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Bullet_Prefab"; b.transform.localScale = Vector3.one * 0.3f;
        Material m = new Material(s); m.color = Color.yellow;
        b.GetComponent<Renderer>().sharedMaterial = m;
        b.GetComponent<Collider>().isTrigger = true;
        b.AddComponent<Bullet>();
        b.transform.SetParent(r);
        b.transform.localPosition = Vector3.down * 100;
        return b;
    }

    private void CreateWall(WallData d, Transform p, string n, Material mat)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = n + "Wall"; w.transform.SetParent(p);
        w.transform.localPosition = new Vector3(d.x, d.h / 2, d.z);
        w.transform.rotation      = Quaternion.Euler(0, d.rot, 0);
        w.transform.localScale    = new Vector3(d.w, d.h, d.l);
        w.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private struct WallData
    {
        public float x, z, w, l, rot, h;
        public WallData(float x, float z, float w, float l, float r, float h = 12f)
        { this.x = x; this.z = z; this.w = w; this.l = l; this.rot = r; this.h = h; }
    }
}

// SimpleCameraFollow was moved to Assets/Scripts/Runtime/SimpleCameraFollow.cs
// so it is available at runtime (Editor-folder classes are editor-only).
