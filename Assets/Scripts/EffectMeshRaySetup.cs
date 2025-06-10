using UnityEngine;
using System.Collections;                            // for IEnumerator
using Oculus.Interaction;                            // for RayInteractable
using Oculus.Interaction.Surfaces;                   // for ColliderSurface
using Meta.XR.MRUtilityKit;                          // for MRUK

public class EffectMeshRaySetup : MonoBehaviour
{
    private IEnumerator Start()
    {
        Debug.Log("EffectMeshRaySetup: Coroutine started.");

        // Wait until MRUK has created its rooms
        yield return new WaitUntil(() =>
        {
            bool ready = MRUK.Instance != null
                      && MRUK.Instance.Rooms     != null
                      && MRUK.Instance.Rooms.Count > 0;
            Debug.Log($"EffectMeshRaySetup: MRUK ready? {ready} (Instance: {MRUK.Instance}, Rooms count: {MRUK.Instance?.Rooms?.Count})");
            return ready;
        });

        Debug.Log($"EffectMeshRaySetup: Found {MRUK.Instance.Rooms.Count} rooms.");

        foreach (var room in MRUK.Instance.Rooms)
        {
            Debug.Log($"EffectMeshRaySetup: Processing room: {room.name}");

            // First level: surfaces like WALL_FACE, FLOOR, etc.
            foreach (Transform surface in room.transform)
            {
                Debug.Log($"EffectMeshRaySetup: Checking surface: {surface.name}");

                // Second level: look for the child named "*_EffectMesh"
                foreach (Transform effectChild in surface)
                {
                    Debug.Log($"EffectMeshRaySetup: Checking child: {effectChild.name}");
                    if (!effectChild.name.EndsWith("_EffectMesh"))
                        continue;

                    Debug.Log($"EffectMeshRaySetup: → Found EffectMesh: {effectChild.name}");
                    var go = effectChild.gameObject;

                    // 1) Ensure there’s a MeshCollider
                    var meshCol = go.GetComponent<MeshCollider>();
                    if (meshCol == null)
                    {
                        meshCol = go.AddComponent<MeshCollider>();
                        Debug.Log("EffectMeshRaySetup:   • Added MeshCollider");
                    }
                    else
                    {
                        Debug.Log("EffectMeshRaySetup:   • Found existing MeshCollider");
                    }
                    meshCol.isTrigger = false;

                    // 2) Add or get the ColliderSurface, then inject the collider
                    var surfComp = go.GetComponent<ColliderSurface>();
                    if (surfComp == null)
                    {
                        surfComp = go.AddComponent<ColliderSurface>();
                        Debug.Log("EffectMeshRaySetup:   • Added ColliderSurface");
                    }
                    else
                    {
                        Debug.Log("EffectMeshRaySetup:   • Found existing ColliderSurface");
                    }
                    surfComp.InjectCollider(meshCol);
                    Debug.Log("EffectMeshRaySetup:   • Injected MeshCollider into ColliderSurface");

                    // 3) Add or get the RayInteractable, then inject the surface
                    var ri = go.GetComponent<RayInteractable>();
                    if (ri == null)
                    {
                        ri = go.AddComponent<RayInteractable>();
                        Debug.Log("EffectMeshRaySetup:   • Added RayInteractable");
                    }
                    else
                    {
                        Debug.Log("EffectMeshRaySetup:   • Found existing RayInteractable");
                    }
                    ri.InjectSurface(surfComp);
                    Debug.Log("EffectMeshRaySetup:   • Injected ColliderSurface into RayInteractable");
                }
            }
        }

        Debug.Log("EffectMeshRaySetup: Setup completed.");
    }
}
