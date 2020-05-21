using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Clayxels;
using Valve.VR;
using TMPro;

public class ClayVRController : MonoBehaviour
{

    public enum ClayVRTool
    {
        MOVE,
        SCALE,
        BLEND,
        COLOR,
        PRIMITIVE,
    }

    public enum ClayVRPrimitive
    {
        CUBE,
        SPHERE,
        CYLINDER,
        TORUS,
        CURVE,
    }

    private SteamVR_Behaviour_Skeleton handSkeleton;
    private SteamVR_Input_Sources inputSource;

    private Transform originalGrabParent;
    private ClayObject grabed = null;

    private ClayObject lastTargetedClayObject;

    [Header("Config")]
    public Transform raycastPoint;
    public float raycastRadius;
    public float raycastLength;

    private Vector3 lastRootPositon;

    [Header("UI")]
    public GameObject uiContainer;
    public GameObject toolContainer;
    private ClayVRTool currentTool;
    private int toolCount;
    private bool justSelectedTool;
    private ClayVRTool selectedTool;
    private SpriteRenderer[] toolSprites;
    public GameObject toolSelector;
    public TextMeshPro toolText;
    public GameObject colorPicker;
    private Material colorPickerMaterial;
    private Vector3 pickedColor;

    private bool colorToolHSV;

    private SteamVR_Action_Boolean grabGrip = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("GrabGrip");
    private SteamVR_Action_Boolean interactButton = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("InteractUI");
    private SteamVR_Action_Vector2 joystickVector = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("Joystick");

    private static float TOOL_SENSIBILITY = 3;
    private static float TOOL_SELECTOR_SIZE = 3.5f;
    private static float DESELECTED_TOOL_SCALE = .4f;
    private static Color DESELECTED_TOOL_COLOR = Color.black;
    private static float SELECTED_TOOL_SCALE = .6f;
    private static Color SELECTED_TOOL_COLOR = Color.green;
    private static string ICON_PATH_PREFIX = "Icons/";

    private void Awake()
    {
        handSkeleton = GetComponent<SteamVR_Behaviour_Skeleton>();
        inputSource = handSkeleton.inputSource;

        toolCount = System.Enum.GetValues(typeof(ClayVRTool)).Length;
        toolSprites = new SpriteRenderer[toolCount];
        float toolAngle = 0;
        for(int toolIndex = 0; toolIndex < toolCount; ++toolIndex)
        {
            float toolSelectionDelta = Mathf.PI * 2f / toolCount;
            string toolName = ((ClayVRTool)toolIndex).ToString();

            Sprite sprite = Resources.Load<Sprite>(ICON_PATH_PREFIX + toolName);
            GameObject toolObject = new GameObject(toolName);
            toolObject.transform.SetParent(toolContainer.transform);
            toolObject.transform.localScale = Vector3.one * DESELECTED_TOOL_SCALE;
            toolObject.transform.localPosition = new Vector3(Mathf.Sin(toolAngle) * TOOL_SELECTOR_SIZE, Mathf.Cos(toolAngle) * TOOL_SELECTOR_SIZE, 0);
            toolObject.transform.localRotation = Quaternion.identity;
            SpriteRenderer spriteRenderer = toolObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = DESELECTED_TOOL_COLOR;
            toolSprites[toolIndex] = spriteRenderer;

            toolAngle += toolSelectionDelta;
        }

        colorPicker.SetActive(false);
        colorPickerMaterial = colorPicker.GetComponent<MeshRenderer>().material;

        ChangeTool(ClayVRTool.MOVE);
    }

    private void ChangeTool(ClayVRTool toolToSet)
    {
        currentTool = toolToSet;
        toolText.text = currentTool.ToString();
    }

    private float Angle(Vector2 vector)
    {
        if (vector.x < 0)
        {
            return Mathf.PI * 2 - (Mathf.Atan2(vector.x, vector.y) * -1);
        }
        else
        {
            return Mathf.Atan2(vector.x, vector.y);
        }
    }

    private void Update()
    {
        uiContainer.transform.LookAt(Camera.main.transform);

        if (lastTargetedClayObject != null)
        {
            MeshRenderer meshRenderer = lastTargetedClayObject.GetComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            lastTargetedClayObject.transform.hasChanged = true;
            lastTargetedClayObject = null;
        }

        GameObject targetObject = null;
        ClayObject targetedClayObject = null;

        // NOTE(Juan): Tool selection
        Vector2 lastJoystickAxis = joystickVector.GetLastAxis(inputSource);
        bool toolSelectionEnabled = grabed == null && lastJoystickAxis.magnitude > 0.5f;
        if (grabed == null)
        {
            for (int toolIndex = 0; toolIndex < toolCount; ++toolIndex)
            {
                toolSprites[toolIndex].gameObject.SetActive(toolSelectionEnabled);
            }

            if (!toolSelectionEnabled && justSelectedTool)
            {
                justSelectedTool = false;
                ChangeTool(selectedTool);
            }

            if (toolSelectionEnabled)
            {
                float toolSelectionDelta = Mathf.PI * 2f / toolCount;
                toolSelector.transform.localPosition = new Vector3(lastJoystickAxis.x * TOOL_SELECTOR_SIZE, lastJoystickAxis.y * TOOL_SELECTOR_SIZE, 0);
                float angle = Angle(lastJoystickAxis);
                int selectedToolIndex = Mathf.RoundToInt(angle / toolSelectionDelta) % toolCount;

                for (int toolIndex = 0; toolIndex < toolCount; ++toolIndex)
                {
                    toolSprites[toolIndex].transform.localScale = Vector3.one * (toolIndex == selectedToolIndex ? SELECTED_TOOL_SCALE : DESELECTED_TOOL_SCALE);
                    toolSprites[toolIndex].color = (toolIndex == selectedToolIndex ? SELECTED_TOOL_COLOR : DESELECTED_TOOL_COLOR);
                }

                selectedTool = (ClayVRTool)selectedToolIndex;
                toolText.text = selectedTool.ToString();
                justSelectedTool = true;
            }
            else
            {
                toolSelector.transform.localPosition = Vector3.zero;
            }

            float maxDistance = raycastLength;
            RaycastHit[] hitResults = new RaycastHit[10];
            int hitCount = Physics.SphereCastNonAlloc(new Ray(raycastPoint.transform.position, raycastPoint.transform.forward),
                raycastRadius, hitResults, maxDistance, ClayVR.instance.objectLayerMask);
            if (hitCount > 0)
            {
                float closestDistance = maxDistance * 10f;
                for(int i = 0; i < hitCount; ++i)
                {
                    float tempDistance = Vector3.Distance(hitResults[i].transform.position, raycastPoint.transform.position);
                    if (tempDistance < closestDistance)
                    {
                        targetObject = hitResults[i].transform.gameObject;
                    }
                }

                targetedClayObject = targetObject.GetComponent<ClayObject>();
                if (targetedClayObject != null)
                {
                    targetedClayObject.transform.hasChanged = true;
                }

                lastTargetedClayObject = targetedClayObject;

                MeshRenderer meshRenderer = lastTargetedClayObject.GetComponent<MeshRenderer>();
                meshRenderer.enabled = true;
                meshRenderer.material = ClayVR.instance.highlightMaterial;
            }
        }
        else
        {
            MeshRenderer meshRenderer = grabed.GetComponent<MeshRenderer>();
            meshRenderer.enabled = true;
            meshRenderer.material = ClayVR.instance.grabbedMaterial;
        }

        if (grabGrip.GetLastStateDown(inputSource) && grabed == null)
        {
            if (targetedClayObject != null)
            {
                originalGrabParent = targetObject.transform.parent;

                if(originalGrabParent.GetComponent<ClayContainer>())
                {
                    targetedClayObject.steppedUpdate = .1f;
                    grabed = targetedClayObject;
                    if(currentTool == ClayVRTool.MOVE)
                    {
                        grabed.transform.SetParent(transform);
                    }
                }
            }
        }
        if (grabGrip.GetLastStateUp(inputSource) && grabed != null)
        {
            if (currentTool == ClayVRTool.MOVE)
            {
                grabed.transform.SetParent(originalGrabParent);
            }
            grabed.steppedUpdate = 0;
            grabed.transform.hasChanged = true;
            grabed = null;
        }

        Vector3 rootPosition = transform.position;
        Vector3 rootDelta = (rootPosition - lastRootPositon) * TOOL_SENSIBILITY;
        lastRootPositon = rootPosition;

        toolText.color = Color.white;
        colorPicker.SetActive(!toolSelectionEnabled && currentTool == ClayVRTool.COLOR);

        if (grabed)
        {
            Vector3 toolDelta = grabed.transform.InverseTransformDirection(rootDelta);

            if (currentTool == ClayVRTool.SCALE)
            {
                Vector3 scale = grabed.transform.localScale;
                scale.x = Mathf.Abs(scale.x + toolDelta.x);
                scale.y = Mathf.Abs(scale.y + toolDelta.y);
                scale.z = Mathf.Abs(scale.z + toolDelta.z);
                grabed.transform.localScale = scale;
            }
            else if (currentTool == ClayVRTool.BLEND)
            {
                grabed.blend = Mathf.Clamp(grabed.blend + toolDelta.y, -1, 1);
                toolText.text = currentTool.ToString() + "\n" + grabed.blend.ToString("0.00");
            }
            else if(currentTool == ClayVRTool.PRIMITIVE)
            {
                if (interactButton.GetLastStateDown(inputSource))
                {
                    grabed.primitiveType = (grabed.primitiveType + 1) % 4;
                    
                    grabed.transform.hasChanged = true;
                }

                if ((ClayVRPrimitive)grabed.primitiveType == ClayVRPrimitive.CUBE)
                {
                    grabed.attrs.x = Mathf.Clamp(grabed.attrs.x + toolDelta.x, -1, 1);

                    toolText.text = currentTool.ToString() + "\n" + ((ClayVRPrimitive)grabed.primitiveType).ToString() + "\n" +
                        string.Format("Round: {0}", grabed.attrs.x.ToString("0.00"));
                }
                else if ((ClayVRPrimitive)grabed.primitiveType == ClayVRPrimitive.CYLINDER)
                {
                    grabed.attrs.x = Mathf.Clamp(grabed.attrs.x + toolDelta.x, -1, 1);
                    grabed.attrs.y = Mathf.Clamp(grabed.attrs.y + toolDelta.y, -1, 1);
                    grabed.attrs.z = Mathf.Clamp(grabed.attrs.z + toolDelta.z, -1, 1);

                    toolText.text = currentTool.ToString() + "\n" + ((ClayVRPrimitive)grabed.primitiveType).ToString() + "\n" +
                        string.Format("Round {0}\nSharp {1}\nCone {2}", grabed.attrs.x.ToString("0.00"), grabed.attrs.y.ToString("0.00"), grabed.attrs.z.ToString("0.00"));
                }
                else if ((ClayVRPrimitive)grabed.primitiveType == ClayVRPrimitive.TORUS)
                {
                    grabed.attrs.x = Mathf.Clamp(grabed.attrs.x + toolDelta.x, -1, 1);

                    toolText.text = currentTool.ToString() + "\n" + ((ClayVRPrimitive)grabed.primitiveType).ToString() + "\n" +
                        string.Format("Fat {0}", grabed.attrs.x.ToString("0.00"));
                }
                else
                {
                    toolText.text = currentTool.ToString() + "\n" + ((ClayVRPrimitive)grabed.primitiveType).ToString();
                }

                grabed.transform.hasChanged = true;
            }
            else if(currentTool == ClayVRTool.COLOR)
            {
                if (colorToolHSV)
                {
                    grabed.color = Color.HSVToRGB(pickedColor.x, pickedColor.y, pickedColor.z);
                }
                else
                {
                    grabed.color = new Color(pickedColor.x, pickedColor.y, pickedColor.z);
                }
            }
        }
        else if(!toolSelectionEnabled)
        {
            Vector3 toolDelta = transform.InverseTransformDirection(rootDelta);
            //if(toolDelta.magnitude > 0)
            //{
            //    Debug.Log(string.Format("X: {0} | Y: {1} | Z: {2}", toolDelta.x.ToString("0.000"), toolDelta.y.ToString("0.000"), toolDelta.z.ToString("0.000")));
            //}

            if (currentTool == ClayVRTool.PRIMITIVE)
            {
                if(interactButton.GetLastStateDown(inputSource))
                {
                    if(targetedClayObject != null)
                    {
                        GameObject pieceObject = ClayVR.ClonePiece(targetedClayObject.gameObject, ClayVR.instance.inEditContainer.transform);
                        pieceObject.transform.position = transform.position;
                    }
                    else {
                        GameObject pieceObject = ClayVR.CreatePiece(ClayVR.instance.inEditContainer.transform);
                        pieceObject.transform.position = transform.position;
                        pieceObject.transform.localScale = Vector3.one * 0.1f;
                    }
                }
            }
            if (currentTool == ClayVRTool.COLOR)
            {
                if (interactButton.GetLastStateDown(inputSource))
                {
                    if(targetedClayObject != null)
                    {
                        pickedColor.x = targetedClayObject.color.r;
                        pickedColor.y = targetedClayObject.color.g;
                        pickedColor.z = targetedClayObject.color.b;
                    }
                    else
                    {
                        if(colorToolHSV)
                        {
                            Color color = Color.HSVToRGB(pickedColor.x, pickedColor.y, pickedColor.z);
                            pickedColor.x = color.r;
                            pickedColor.y = color.g;
                            pickedColor.z = color.b;
                        }
                        else
                        {
                            Color color = new Color(pickedColor.x, pickedColor.y, pickedColor.z);
                            Color.RGBToHSV(color, out pickedColor.x, out pickedColor.y, out pickedColor.z);
                        }

                        colorToolHSV = !colorToolHSV;
                    }
                }

                if(grabGrip.GetState(inputSource))
                {
                    pickedColor.x = Mathf.Clamp(pickedColor.x + toolDelta.x, 0, 1); // R / HUE
                    pickedColor.y = Mathf.Clamp(pickedColor.y + toolDelta.y, 0, 1); // G / SATURATION
                    pickedColor.z = Mathf.Clamp(pickedColor.z + toolDelta.z, 0, 1); // B / VALUE
                }

                toolSelector.transform.localPosition = new Vector3((pickedColor.x * 2 - 1) * TOOL_SELECTOR_SIZE, (pickedColor.y * 2 - 1) * TOOL_SELECTOR_SIZE, 0);

                if (colorToolHSV)
                {
                    colorPickerMaterial.SetFloat("_ZCoord", pickedColor.z);
                    colorPickerMaterial.SetFloat("_HSV", 1);

                    toolText.text = currentTool.ToString() + "\n" + string.Format("H: {0}, S: {1}, V: {2}", pickedColor.x.ToString("0.00"), pickedColor.y.ToString("0.00"), pickedColor.z.ToString("0.00"));
                    toolText.color = Color.HSVToRGB(pickedColor.x, pickedColor.y, pickedColor.z);
                }
                else
                {
                    colorPickerMaterial.SetFloat("_ZCoord", pickedColor.z);
                    colorPickerMaterial.SetFloat("_HSV", -1);

                    toolText.text = currentTool.ToString() + "\n" + string.Format("R: {0}, G: {1}, B: {2}", pickedColor.x.ToString("0.00"), pickedColor.y.ToString("0.00"), pickedColor.z.ToString("0.00"));
                    toolText.color = new Color(pickedColor.x, pickedColor.y, pickedColor.z);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(raycastPoint.transform.position, raycastRadius);
        Gizmos.DrawLine(raycastPoint.transform.position, raycastPoint.transform.position + raycastPoint.transform.forward * raycastLength);
    }
    
}
