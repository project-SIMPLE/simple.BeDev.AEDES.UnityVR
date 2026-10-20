using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class Test : MonoBehaviour
{
    private InputDevice _controller;

    void Start()
    {
        TryInitialize();
    }

    void TryInitialize()
    {
        var devices = new List<InputDevice>();
        var characteristics = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        if (devices.Count > 0)
        {
            _controller = devices[0];
        }
    }

    void Update()
    {
        if (!_controller.isValid)
        {
            TryInitialize();
        }
    }
}
