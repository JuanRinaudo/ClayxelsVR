using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Clayxels;

public class ClayVR : MonoBehaviour
{

    public static ClayVR instance;

    [Header("Prefabs")]
    public GameObject clayContainerPrefab;
    public GameObject clayObjectPrefab;

    [Header("Materials")]
    public Material highlightMaterial;
    public Material grabbedMaterial;

    [Space]
    public ClayContainer inEditContainer;

    [HideInInspector]
    public LayerMask objectLayerMask;
    [HideInInspector]
    public LayerMask containerLayerMask;

    [MenuItem("Tools/SaveClayxels")]
    public static void SaveClayxels()
    {
        GameObject mainContainer = GameObject.Find("ToSave");
        string assetFolder = "Assets/SaveClay/";
        CheckAndGenerateAssetsFolder(assetFolder);
        PrefabUtility.SaveAsPrefabAsset(mainContainer, assetFolder + mainContainer.name + ".prefab");
    }

    public static bool CheckAndGenerateAssetsFolder(string path)
    {
        string[] pathParts = path.Split('/');

        int startIndex = 0;
        string runningPath = "";
        if (pathParts[0] == "Assets")
        {
            startIndex = 1;
            runningPath = "Assets";
        }

        for (int pathIndex = startIndex; pathIndex < pathParts.Length; ++pathIndex)
        {
            if (pathParts[pathIndex] != "")
            {
                if (!AssetDatabase.IsValidFolder(runningPath + "/" + pathParts[pathIndex]))
                {
                    AssetDatabase.CreateFolder(runningPath, pathParts[pathIndex]);
                }

                runningPath += "/" + pathParts[pathIndex];
            }
        }

        return true;
    }

    private void Awake()
    {
        if(instance != null)
        {
            Destroy(this);
            return;
        }

        instance = this;

        inEditContainer = FindObjectOfType<ClayContainer>();

        objectLayerMask = LayerMask.GetMask("ClayObject");
        if (objectLayerMask == 0)
        {
            Debug.LogError("ClayVR Error - There is no ClayObject layer, create it.");
        }

        containerLayerMask = LayerMask.GetMask("ClayContainer");
        if (objectLayerMask == 0)
        {
            Debug.LogError("ClayVR Error - There is no ClayContainer layer, create it.");
        }
    }

    public static GameObject CreatePiece(Transform parent)
    {
        return Instantiate(instance.clayObjectPrefab, parent);
    }

    public static GameObject ClonePiece(GameObject instance, Transform parent)
    {
        return Instantiate(instance, parent);
    }

}
