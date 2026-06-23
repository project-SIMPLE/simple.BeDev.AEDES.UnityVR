using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class IntroUI : MonoBehaviour
{
    public bool R_primaryValue, L_primaryValue, R_secondary, L_secondary, R_gripValue, L_gripValue, R_triggerValue, L_triggerValue, IsMoveL, IsMoveR;
    public UnityEngine.XR.InputDevice _rightController;
    public UnityEngine.XR.InputDevice _leftController;
    public UnityEngine.XR.InputDevice _HMD;

    public GameObject[] UIs;
    public int currentUI;

    public bool checkreturn, asd, returnValue;

    public GameObject Menucanvas;
    private void Start()
    {

    }
    private void Update()
    {
        checkinput();

    }
    private void FixedUpdate()
    {
        if (onclick(R_primaryValue) )
        {
            if (Menucanvas.transform.position.z > -0.5)
            {
                Menucanvas.transform.position = new Vector3(0, -0, -11);
            }
            for (int i = 0; i < UIs.Length; i++)
            {
                UIs[i].SetActive(false);
            }
            currentUI++;
            if (currentUI > UIs.Length-1)
            {
                currentUI = 0;
            }
            print(currentUI);
            UIs[currentUI].SetActive(true);
        }
    }
    public bool onclick(bool Bool)
    {
        if (Bool && checkreturn)
        {
            returnValue = true;
            checkreturn = false;
        }
        else if (Bool && !checkreturn)
        {
            returnValue = false;
            checkreturn = false;
        }
        else if (!Bool)
        {
            returnValue = false;
            checkreturn = true;
        }
        return returnValue;

    }
    public void checkinput()
    {
        if (!_rightController.isValid || !_leftController.isValid || !_HMD.isValid)
            InitializeInputDevices();
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out R_triggerValue) && R_triggerValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out L_triggerValue) && L_triggerValue)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out R_gripValue) && R_gripValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out L_gripValue) && L_gripValue)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out R_secondary) && R_secondary)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out L_secondary) && L_secondary)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out R_primaryValue) && R_primaryValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out L_primaryValue) && L_primaryValue)
        {

        }
    }
    private void InitializeInputDevices()
    {

        if (!_rightController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, ref _rightController);
        }
        if (!_leftController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, ref _leftController);
        }
        if (!_HMD.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.HeadMounted, ref _HMD);
        }

    }
    private void InitializeInputDevice(InputDeviceCharacteristics inputCharacteristics, ref UnityEngine.XR.InputDevice inputDevice)
    {
        List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(inputCharacteristics, devices);
        if (devices.Count > 0)
        {
            inputDevice = devices[0];
        }
    }
}
