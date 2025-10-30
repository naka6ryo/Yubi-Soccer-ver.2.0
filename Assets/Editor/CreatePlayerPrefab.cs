using UnityEditor;
using UnityEngine;
using Photon.Pun;

// Editor helper: creates Assets/Prehabs/Player.prefab with PhotonView and PlayerController.
// Run from menu: Tools/Create Player Prefab
public class CreatePlayerPrefab
{
    [MenuItem("Tools/Create Player Prefab")]
    public static void Create()
    {
        // ensure folder exists
        var dir = "Assets/Prehabs";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets", "Prehabs");
        }

        // create root object
        var go = new GameObject("Player");
        // add visible collider/renderer for testing
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.SetParent(go.transform, false);

        // add PlayerController
        var playerController = go.AddComponent<PlayerController>();

        // add PhotonView
        var pv = go.AddComponent<PhotonView>();
        // set observed components to include PlayerController so serialization is called
        pv.ObservedComponents = new System.Collections.Generic.List<Component> { playerController };

        // add a child camera for local player
        var camGO = new GameObject("PlayerCamera");
        camGO.transform.SetParent(go.transform, false);
        var cam = camGO.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.transform.localPosition = new Vector3(0, 1.2f, -2f);
        cam.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        cam.gameObject.SetActive(false);

        playerController.playerCamera = cam;

        // save as prefab in Assets/Prehabs
        var prefabPath = "Assets/Prehabs/Player.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.UserAction);

        // Also save a copy under Resources so PhotonNetwork.Instantiate("Player", ...) can find it at runtime
        var resourcesDir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        var resourcesPath = "Assets/Resources/Player.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, resourcesPath, InteractionMode.UserAction);

        // cleanup scene object
        Object.DestroyImmediate(go);

        Debug.Log("Created Player prefabs at " + prefabPath + " and " + resourcesPath + ". Attach/adjust components as needed.");
    }
}
