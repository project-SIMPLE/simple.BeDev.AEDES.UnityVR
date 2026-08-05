using System.Collections.Generic;

using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.iOS;
using UnityEngine.tvOS;
public class InputVR : MonoBehaviour
{
    public UnityEngine.XR.InputDevice _rightController;
    public UnityEngine.XR.InputDevice _leftController;
    public UnityEngine.XR.InputDevice _HMD;


    void Update()
    {
        if (!_rightController.isValid || !_leftController.isValid || !_HMD.isValid)
            InitializeInputDevices();
        bool triggerValue;
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out triggerValue) && triggerValue)
        {
            Debug.Log("Trigger button is pressed.");
        }
    }
    private void InitializeInputDevices()
    {

        if (!_rightController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, ref _rightController);
            print("A");
        }
        if (!_leftController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, ref _leftController);
            print("S");
        }
        if (!_HMD.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.HeadMounted, ref _HMD);
            print("D");
        }

    }

    private void InitializeInputDevice(InputDeviceCharacteristics inputCharacteristics, ref UnityEngine.XR.InputDevice inputDevice)
    {
        List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
        //Call InputDevices to see if it can find any devices with the characteristics we're looking for
        InputDevices.GetDevicesWithCharacteristics(inputCharacteristics, devices);

        //Our hands might not be active and so they will not be generated from the search.
        //We check if any devices are found here to avoid errors.
        if (devices.Count > 0)
        {
            inputDevice = devices[0];
        }
    }
}