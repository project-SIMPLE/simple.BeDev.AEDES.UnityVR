using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class PlayerMain : MonoBehaviour
{
    public static PlayerMain instance;
    public UnityEngine.XR.InputDevice _rightController;
    public UnityEngine.XR.InputDevice _leftController;
    public UnityEngine.XR.InputDevice _HMD;
    public Slider BloodBar,NectarBar;

    public Rigidbody rb;

    public GameObject mainCamera, CamRot;
    public GameObject termalobj,ui;

    public float Speed, FlyUpSpeed,CamRotSpeed;
    public float Current_Blood, Max_Blood;
    public float Current_Nec, Max_Nec;

    public bool R_primaryValue,L_primaryValue, R_secondary, L_secondary, R_gripValue,L_gripValue, R_triggerValue, L_triggerValue,IsMoveL,IsMoveR;
    public bool termalmode,canmove;
    public bool isMate,Death,RestartAble;

    public int EggLayed;

    public Vector2 L_moveInput, R_moveInput;
    public SendReceiveMessageExample sr;

    private void Awake()
    {
        canmove = true;
        instance = this;
        rb = GetComponent<Rigidbody>();
        BloodBar.maxValue = Max_Blood;
        BloodBar.value = Current_Blood;
        NectarBar.maxValue = Max_Nec;
        Current_Nec = Max_Nec/2;
        NectarBar.value = Current_Nec;
    }

    void Update()
    {
        
        checkinput();
        if (!Death||GameManager.instance.time>0)
        {
            termalobj.SetActive(R_triggerValue);
            Move(L_moveInput);
            if (L_gripValue)
            {
                GameManager.instance.questUI.SetActive(L_gripValue);
            }
            NectarUPdate();
        }
    }

    private void FixedUpdate()
    {

        ui.transform.eulerAngles = new Vector3(0, mainCamera.transform.eulerAngles.y, 0);
    }
    public void NectarUPdate()
    {
        if (Current_Nec > 0)
        {
            Current_Nec -= Time.deltaTime / Max_Nec;
            NectarBar.value = Current_Nec;
        }
        else
        {
            GameManager.instance.GameOver();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.GetComponent<Pond>())
        {
            if (Current_Blood >= Max_Blood&&isMate)
            {
                if (R_primaryValue)
                {
                    Current_Blood = 0;
                    BloodBar.value = Current_Blood;
                    EggLayed++;
                    GameManager.instance.setscore(other.gameObject.GetComponent<Pond>().Score);
                }
            }
        }
        if (other.gameObject.GetComponent<Wild_Mosquitos>())
        {
            if (other.gameObject.GetComponent<Wild_Mosquitos>().Gender == Wild_Mosquitos.genderlist.male && !isMate)
            {
                if (R_primaryValue)
                {
                    isMate = true;
                }
            }
        }
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
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out L_moveInput) && IsMoveL)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out R_moveInput) && IsMoveR)
        {

        }
        if (RestartAble)
        {
            if(R_primaryValue || L_primaryValue || R_secondary || L_secondary || R_gripValue || L_gripValue || R_triggerValue || L_triggerValue || IsMoveL || IsMoveR)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }

    public void Move(Vector2 direction)
    {
        if (canmove)
        {
            Vector3 forward = mainCamera.transform.forward;
            Vector3 right = mainCamera.transform.right;
            forward.y = 0;
            forward.Normalize();
            transform.Translate(0, R_moveInput.y * Time.deltaTime * FlyUpSpeed, 0);
            Vector3 moveDirection = (forward * direction.y + right * direction.x).normalized;
            rb.linearVelocity = moveDirection * Speed;
            CamRot.transform.Rotate(0, R_moveInput.x * CamRotSpeed, 0);
        }
    }

    public void Drink()
    {
        if(!Death || GameManager.instance.time > 0)
        {
            Current_Blood += Time.deltaTime;
            BloodBar.value = Current_Blood;
            canmove = !R_primaryValue;
        }
    }
    public void DrinkNectar()
    {
        if (!Death || GameManager.instance.time > 0)
        {
            Current_Nec += Time.deltaTime;
            NectarBar.value = Current_Nec;
            canmove = !R_primaryValue;
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
