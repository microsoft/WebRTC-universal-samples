using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WorldCursor : MonoBehaviour
{
    private MeshRenderer meshRenderer;

    public Button ConnectButton;
    public Button CallButton;

    private bool connectButtonSelected = false;
    private bool callButtonSelected = false;

    // Use this for initialization
    void Start()
    {
        // Grab the mesh renderer that's on the same object as this script.
        meshRenderer = this.gameObject.GetComponentInChildren<MeshRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        // Do a raycast into the world based on the user's
        // head position and orientation.
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        RaycastHit hitInfo;

        if (Physics.Raycast(headPosition, gazeDirection, out hitInfo))
        {
            // If the raycast hit a hologram...
            // Display the cursor mesh.
            meshRenderer.enabled = true;

            // Move thecursor to the point where the raycast hit.
            this.transform.position = hitInfo.point;

            // Rotate the cursor to hug the surface of the hologram.
            this.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
        }
        else
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = new Vector2(headPosition.x, headPosition.y);
            List<RaycastResult> objects = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, objects);
            if (objects.Count > 0)
            {
                bool buttonGazed = false;
                GameObject firstGameObject = null;
                System.Diagnostics.Debug.WriteLine("WorldCursor - RaycastAll objects hit");
                foreach (RaycastResult result in objects)
                {
                    if (firstGameObject == null)
                        firstGameObject = result.gameObject;
                    if (result.gameObject.GetComponent<Button>() == ConnectButton)
                    {
                        connectButtonSelected = true;
                        buttonGazed = true;
                        ColorBlock colorBlock = ConnectButton.colors;
                        colorBlock.normalColor = Color.red;
                        ConnectButton.colors = colorBlock;
                    }
                    else if (result.gameObject.GetComponent<Button>() == CallButton)
                    {
                        callButtonSelected = true;
                        buttonGazed = true;
                        ColorBlock colorBlock = CallButton.colors;
                        colorBlock.normalColor = Color.red;
                        CallButton.colors = colorBlock;
                    }
                }
                if (!buttonGazed)
                {
                    if (connectButtonSelected)
                    {
                        connectButtonSelected = false;
                        ColorBlock colorBlock = ConnectButton.colors;
                        colorBlock.normalColor = Color.white;
                        ConnectButton.colors = colorBlock;
                    }
                    if (callButtonSelected)
                    {
                        callButtonSelected = false;
                        ColorBlock colorBlock = CallButton.colors;
                        colorBlock.normalColor = Color.white;
                        CallButton.colors = colorBlock;
                    }
                }
                this.transform.position = new Vector3(firstGameObject.transform.position.x, firstGameObject.transform.position.y, -40);
                meshRenderer.enabled = true;
            }
            else
            {
                // If the raycast did not hit a hologram, hide the cursor mesh.
                meshRenderer.enabled = false;
            }
        }
    }
}