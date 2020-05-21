using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ReferenceData
{
    public GameObject instance;
    public MeshRenderer renderer;
    public Texture texture;
    public Vector3 offset;
    public Vector2 scale;
    public Quaternion rotation;
    public bool keepRatio;
    public bool lookAtCenter;
}

public class ReferenceSystem : MonoBehaviour
{

    public List<ReferenceData> references;

    public GameObject referencePrefab;

    void Start()
    {
        references = new List<ReferenceData>();

        CreateNewReference(Quaternion.Euler(0, 90, 0));
        CreateNewReference(Quaternion.Euler(0, 0, 0));
        //CreateNewReference(Quaternion.Euler(-90, 0, 0));

        RefreshReferences();
    }

    private ReferenceData CreateNewReference()
    {
        return CreateNewReference(Vector3.zero, Quaternion.identity);
    }

    private ReferenceData CreateNewReference(Quaternion rotation)
    {
        return CreateNewReference(Vector3.zero, rotation);
    }

    private ReferenceData CreateNewReference(Vector3 offset, Quaternion rotation)
    {
        ReferenceData reference = new ReferenceData();
        reference.instance = Instantiate(referencePrefab, transform);
        reference.renderer = reference.instance.GetComponent<MeshRenderer>();
        reference.texture = reference.renderer.material.mainTexture;
        reference.offset = offset;
        reference.scale = Vector2.one;
        reference.rotation = rotation;
        reference.keepRatio = true;
        reference.lookAtCenter = false;

        references.Add(reference);
        return reference;
    }

    void Update()
    {
        
    }

    [ContextMenu("Refresh References")]
    public void RefreshReferences()
    {
        for(int referenceIndex = 0; referenceIndex < references.Count; ++referenceIndex)
        {
            ReferenceData reference = references[referenceIndex];
            reference.instance.transform.localPosition = reference.offset;
            reference.renderer.material.mainTexture = reference.texture;
            
            if(reference.keepRatio)
            {
                float ratio = (float)reference.texture.height / (float)reference.texture.width;
                reference.instance.transform.localScale = new Vector3(reference.scale.x, reference.scale.x * ratio, -1);
            }
            else
            {
                reference.instance.transform.localScale = new Vector3(reference.scale.x, reference.scale.y, -1);
            }

            if(reference.lookAtCenter)
            {
                reference.instance.transform.LookAt(Vector3.zero);
            }
            else
            {
                reference.instance.transform.localRotation = reference.rotation;
            }
        }
    }

}
