#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configura as baratas como GameObjects reais na cópia TesteDanillo.
/// A UI Toolkit continua responsável apenas pela HUD; as baratas são
/// Sprites/Colliders/Create componentsSerialized na Scene.
/// </summary>
public static class TesteDanilloCockroachSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Scene/TesteDanillo.unity";
    private const string RootName = "CockroachScene";

    [MenuItem("Tools/Clickergochi/Configure TesteDanillo Scene Cockroaches")]
    public static void Configure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ConfigureScene(scene);
    }

    [MenuItem("Tools/Clickergochi/Rebuild TesteDanillo Scene Cockroaches")]
    public static void RebuildActiveScene()
    {
        ConfigureScene(SceneManager.GetActiveScene());
    }

    private static void ConfigureScene(Scene scene)
    {
        GameObject oldManager = FindRoot(scene, "CockroachManager")
            ?? FindRoot(scene, "Legacy_CockroachManager_Disabled");
        if (oldManager != null)
            Object.DestroyImmediate(oldManager);

        SlimeManager slimeManager = UnityEngine.Object.FindFirstObjectByType<SlimeManager>();
        if (slimeManager == null)
        {
            GameObject systems = FindRoot(scene, "PetSystems");
            if (systems == null)
                systems = new GameObject("PetSystems");
            slimeManager = systems.GetComponent<SlimeManager>();
            if (slimeManager == null)
                slimeManager = systems.AddComponent<SlimeManager>();
        }

        // O painel de debug não pode zerar o save ao entrar em Play Mode.
        SlimeDebugPanel debugPanel = UnityEngine.Object.FindFirstObjectByType<SlimeDebugPanel>();
        if (debugPanel != null)
        {
            SerializedObject serializedDebug = new SerializedObject(debugPanel);
            SerializedProperty startAtMin = serializedDebug.FindProperty("startAtMin");
            if (startAtMin != null)
            {
                startAtMin.boolValue = false;
                serializedDebug.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(debugPanel);
            }
        }

        GameObject root = FindRoot(scene, RootName);
        if (root == null)
            root = new GameObject(RootName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        CockroachSceneManager manager = root.GetComponent<CockroachSceneManager>();
        if (manager == null)
            manager = root.AddComponent<CockroachSceneManager>();
        manager.slimeManager = slimeManager;
        manager.maxCritters = 3;
        manager.hitsRequired = 5;
        manager.hygienePerKill = 0.25f;
        manager.moveSpeedMin = 45f;
        manager.moveSpeedMax = 95f;
        manager.edgePadding = 1.1f;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/2D/Barata.png");
        CockroachUnit[] units = new CockroachUnit[4];
        Vector3[] positions =
        {
            new Vector3(-1.6f, 1.2f, 0f),
            new Vector3(0.8f, 0f, 0f),
            new Vector3(1.6f, -1.2f, 0f),
            new Vector3(-0.8f, -1.8f, 0f)
        };

        for (int i = 0; i < units.Length; i++)
        {
            Transform child = root.transform.Find("Roach_" + i);
            if (child == null)
            {
                GameObject go = new GameObject("Roach_" + i, typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(CockroachUnit));
                child = go.transform;
                child.SetParent(root.transform, false);
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 1;

            CircleCollider2D collider = child.GetComponent<CircleCollider2D>();
            if (collider == null)
                collider = child.gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = false;
            collider.radius = 2f;

            CockroachUnit unit = child.GetComponent<CockroachUnit>();
            if (unit == null)
                unit = child.gameObject.AddComponent<CockroachUnit>();

            child.localPosition = positions[i];
            child.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-12f, 12f));
            child.localScale = Vector3.one * 0.08f;
            unit.Configure(manager, renderer, manager.hitsRequired);
            child.gameObject.SetActive(true);
            units[i] = unit;
        }

        manager.roaches = units;
        EditorUtility.SetDirty(manager);
        foreach (CockroachUnit unit in units)
            if (unit != null) EditorUtility.SetDirty(unit);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("Baratas configuradas como GameObjects na cena TesteDanillo. A UI não cria mais baratas.");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }
}
#endif
