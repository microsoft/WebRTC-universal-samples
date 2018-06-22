using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.WSA.Input;

public class GazeGestureManager : MonoBehaviour
{
    public static GazeGestureManager Instance { get; private set; }

    // Represents the hologram that is currently being gazed at.
    public GameObject FocusedObject { get; private set; }

    GestureRecognizer recognizer;

    public Button ConnectButton;
    public Button CallButton;

    // Use this for initialization
    void Start()
    {
        Instance = this;

        // Set up a GestureRecognizer to detect Select gestures.
        recognizer = new GestureRecognizer();
        recognizer.Tapped += (args) =>
        {
            // Send an OnSelect message to the focused object and its ancestors.
            if (FocusedObject != null)
            {
                if (FocusedObject.GetComponent<Button>() == ConnectButton)
                {
                    ControlScript.Instance.OnConnectClick();
                }
                else if (FocusedObject.GetComponent<Button>() == CallButton)
                {
                    ControlScript.Instance.OnCallClick();
                }
            }
        };
        recognizer.StartCapturingGestures();
    }

    // Update is called once per frame
    void Update()
    {
        // Figure out which hologram is focused this frame.
        GameObject oldFocusObject = FocusedObject;

        // Do a raycast into the world based on the user's
        // head position and orientation.
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        RaycastHit hitInfo;
        if (Physics.Raycast(headPosition, gazeDirection, out hitInfo))
        {
            // If the raycast hit a hologram, use that as the focused object.
            FocusedObject = hitInfo.collider.gameObject;
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
                foreach (RaycastResult result in objects)
                {
                    if (result.gameObject.GetComponent<Button>() == ConnectButton)
                    {
                        buttonGazed = true;
                        FocusedObject = result.gameObject;
                    }
                    else if (result.gameObject.GetComponent<Button>() == CallButton)
                    {
                        buttonGazed = true;
                        FocusedObject = result.gameObject;
                    }
                    System.Diagnostics.Debug.WriteLine(result.gameObject.name + " " + this.transform.position);
                }
                if (!buttonGazed)
                    FocusedObject = null;
            }
            else
            {
                // If the raycast did not hit a hologram, clear the focused object.
                FocusedObject = null;
            }
        }

        // If the focused object changed this frame,
        // start detecting fresh gestures again.
        if (FocusedObject != oldFocusObject)
        {
            recognizer.CancelGestures();
            recognizer.StartCapturingGestures();
        }
    }
}