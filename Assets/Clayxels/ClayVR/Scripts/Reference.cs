using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ReferenceSide
{
    SIDE,
    TOP
}

public class Reference : MonoBehaviour
{

    public ReferenceSide side = ReferenceSide.SIDE;
    public Material material;

    public static float minDotProduct = 0.2f;

    private void Awake()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        material = renderer.material;
    }

    void Update()
    {
        float dot = 0;
        if (side == ReferenceSide.SIDE)
        {
            Vector2 referenceToCamera = new Vector2(transform.forward.normalized.x, transform.forward.normalized.z);//(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z) - new Vector2(transform.position.x, transform.position.z)).normalized;
            Vector2 cameraFoward = new Vector2(Camera.main.transform.forward.normalized.x, Camera.main.transform.forward.normalized.z);
            dot = Vector2.Dot(referenceToCamera, cameraFoward);
        }
        else
        {

        }

        float remapedDot = (Mathf.Abs(dot) - minDotProduct) * (1 / (1 - minDotProduct));
        if (Mathf.Abs(dot) < minDotProduct)
        {
            remapedDot = 0f;
        }
        material.color = new Color(1f, 1f, 1f, remapedDot);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 referenceToCamera = (Camera.main.transform.position - transform.position).normalized;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + referenceToCamera);
    }

}
