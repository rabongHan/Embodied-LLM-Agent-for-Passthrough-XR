// AvatarSpawner.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

using System.Collections;
using Oculus.Interaction;
using Oculus.Interaction.Grab;      // for GrabInteractable
using Oculus.Interaction;

public class AvatarSpawner : MonoBehaviour
{
    [Header("Pointer Settings")]
    public Transform pointerOrigin;
    public float surfaceOffset = 0.05f;
    public RawImage warningImage;

    [Header("Input")]
    public OVRInput.Button     spawnButton = OVRInput.Button.Two;
    public OVRInput.Controller spawnCtrl   = OVRInput.Controller.LTouch;

    void Update()
    {
        if (!OVRInput.GetDown(spawnButton, spawnCtrl))
            return;

        var settings = AvatarSettingsManager.Instance;

        // 1) Block if an avatar already exists
        if (settings.currentInstance != null)
        {
            Debug.Log("Spawn blocked: an avatar is already in the scene.");
            StartCoroutine(ShowWarning());
            return;
        }

        // 2) Raycast under the reticle
        var ray = new Ray(pointerOrigin.position, pointerOrigin.forward);
        if (!Physics.Raycast(ray, out var hit))
            return;

        // 3) Record hit info for replacements
        settings.lastSpawnHitPoint  = hit.point;
        settings.lastSpawnHitNormal = hit.normal;

        // 4) Spawn & align
        var go = SpawnAndAlignAvatarAt(hit.point, hit.normal);
        settings.RegisterNewInstance(go);
        Debug.Log($"Spawned avatar '{go.name}'");

        // 5) Configure lip‐sync for the newly spawned avatar
        settings.ConfigureLipSync(go);
    }
    
    private IEnumerator ShowWarning()
    {
        warningImage.gameObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        warningImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Instantiates currentPrefab and aligns its feet to the surface at hitPoint/normal.
    /// </summary>
    public GameObject SpawnAndAlignAvatarAt(Vector3 hitPoint, Vector3 hitNormal)
    {
        var settings = AvatarSettingsManager.Instance;

        // A) Compute look‐at rotation
        Vector3 camPos = Camera.main.transform.position;
        Vector3 lookDir = Vector3.ProjectOnPlane(camPos - hitPoint, hitNormal).normalized;
        Quaternion rot = Quaternion.LookRotation(lookDir, hitNormal);

        // B) Instantiate at hitPoint
        var go = Instantiate(settings.currentPrefab, hitPoint, rot);

        // C) Compute combined world‐space bounds
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                bounds.Encapsulate(rends[i].bounds);

            // D) Lift so bounds.min.y matches hitPoint.y + surfaceOffset
            float bottomY = bounds.min.y;
            float targetY = hitPoint.y + surfaceOffset;
            float deltaY = targetY - bottomY;
            go.transform.position += Vector3.up * deltaY;

            // E) Apply per-avatar extra tweak
            bool isFirst = (settings.currentPrefab == settings.avatarPrefab1);
            float extraY = isFirst ? settings.extraYOffset1 : settings.extraYOffset2;
            go.transform.position += Vector3.up * extraY;
        }
        else
        {
            // fallback
            go.transform.position = hitPoint + hitNormal * surfaceOffset;
        }

        return go;
    }
}
