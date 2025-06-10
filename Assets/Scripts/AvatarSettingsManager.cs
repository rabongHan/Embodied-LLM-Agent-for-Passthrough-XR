// AvatarSettingsManager.cs
using UnityEngine;
// import your lip-sync components
using uLipSync;

public class AvatarSettingsManager : MonoBehaviour
{
    public static AvatarSettingsManager Instance { get; private set; }
    
    [Header("Avatar Prefabs")]
    public GameObject avatarPrefab1;  // e.g. Robot
    public GameObject avatarPrefab2;  // e.g. Alya

    [Header("Per-Avatar Extra Y Offset")]
    public float extraYOffset1 = 0f;
    public float extraYOffset2 = 0f;

    [HideInInspector] public GameObject currentPrefab;
    [HideInInspector] public GameObject currentInstance;
    [HideInInspector] public Vector3 lastSpawnHitPoint;
    [HideInInspector] public Vector3 lastSpawnHitNormal;

    public Vector3 originalScale = Vector3.one; 
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // pick prefab1 by default
        currentPrefab = avatarPrefab1;
    }

    public void RegisterNewInstance(GameObject go)
    {
        currentInstance = go;
        originalScale   = go.transform.localScale;
    }

    /// <summary>
    /// Called by your UI toggles (pass 1 or 2).
    /// </summary>
    public void SelectAvatar(int index)
    {
        currentPrefab = (index == 1) ? avatarPrefab1 : avatarPrefab2;
        Debug.Log($"Avatar selection → prefab #{index}");

        if (currentInstance != null)
        {
            Destroy(currentInstance);

            // respawn at the same spot/orientation
            var spawner = FindObjectOfType<AvatarSpawner>();
            if (spawner == null)
            {
                Debug.LogError("SelectAvatar: no AvatarSpawner in scene");
                return;
            }

            var go = spawner.SpawnAndAlignAvatarAt(
                lastSpawnHitPoint,
                lastSpawnHitNormal
            );
            RegisterNewInstance(go);
            Debug.Log($"Replaced avatar with prefab #{index}");

            ConfigureLipSync(go);

            if (DemoIntegration.Instance != null && DemoIntegration.Instance.avatarScaleSlider != null)
                DemoIntegration.Instance.avatarScaleSlider.value = 1f;
        }
    }

    /// <summary>
    /// Instead of subscribing/removing listeners, we've already
    /// hooked up both pipelines in the Inspector under the
    /// U Lip Sync component’s “On Lip Sync Update” event:
    ///
    ///   • uLipSyncTexture.OnLipSyncUpdate  
    ///   • uLipSyncBlendShape.OnLipSyncUpdate
    ///
    /// This method simply turns one on, and the other off,
    /// then pieces in the correct Renderer/SkinnedMeshRenderer.
    /// </summary>
    public void ConfigureLipSync(GameObject avatarInstance)
    {
        // find the Audio object
        var audioObj = GameObject.Find("RealtimeAPI/Audio");
        if (audioObj == null)
        {
            Debug.LogError("ConfigureLipSync: can't find RealtimeAPI/Audio");
            return;
        }

        // grab both pipelines
        var texPipe = audioObj.GetComponent<uLipSyncTexture>();
        var blendPipe = audioObj.GetComponent<uLipSyncBlendShape>();
        if (texPipe == null || blendPipe == null)
        {
            Debug.LogError("ConfigureLipSync: missing uLipSyncTexture or uLipSyncBlendShape");
            return;
        }

        bool isRobot = (currentPrefab == avatarPrefab1);

        // enable just the one pipeline we want
        texPipe.enabled = isRobot;
        blendPipe.enabled = !isRobot;

        // assign the correct renderer
        if (isRobot)
        {
            var cyl = avatarInstance.transform.Find("Cylinder.007");
            if (cyl == null)
                Debug.LogError("ConfigureLipSync: 'Cylinder.007' not found on Robot");
            else
                texPipe.targetRenderer = cyl.GetComponent<Renderer>();
        }
        else
        {
            var body = avatarInstance.transform.Find("Тело");
            if (body == null)
                Debug.LogError("ConfigureLipSync: 'Тело' not found on Alya");
            else
                blendPipe.skinnedMeshRenderer = body.GetComponent<SkinnedMeshRenderer>();
        }

        Debug.Log($"ConfigureLipSync: {(isRobot ? "Texture" : "BlendShape")} pipeline active");
    }
    
    public void ScaleCurrentAvatar(float scale)
    {
        if (currentInstance == null)
            return;

        // 1) Apply the new uniform scale
        currentInstance.transform.localScale = originalScale * scale;

        // 2) Reposition so its feet stay on the last hit-point
        //    Compute the mesh’s world-space bottom after scaling
        var rends = currentInstance.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                bounds.Encapsulate(rends[i].bounds);

            float bottomY = bounds.min.y;
            float targetY = lastSpawnHitPoint.y;            // floor level
            float deltaY  = targetY - bottomY;

            currentInstance.transform.position += Vector3.up * deltaY;
        }
    }
}
