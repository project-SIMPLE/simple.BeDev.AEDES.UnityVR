using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
public class PlayerMain : MonoBehaviour
{
    public static PlayerMain instance;
    public UnityEngine.XR.InputDevice _rightController;
    public UnityEngine.XR.InputDevice _leftController;
    public UnityEngine.XR.InputDevice _HMD;

    public Rigidbody rb;

    public GameObject mainCamera, CamRot;
    public GameObject termalcam;

    public float Speed, FlyUpSpeed,CamRotSpeed;
    public float Current_Blood, Max_Blood;
    public float Current_Nec, Max_Nec;

    public bool R_primaryValue,L_primaryValue, R_secondary, L_secondary, R_gripValue,L_gripValue, R_triggerValue, L_triggerValue,IsMoveL,IsMoveR;
    public bool termalmode,canmove;
    public bool isMate,Death,RestartAble;

    public int EggLayed;

    public Vector2 L_moveInput, R_moveInput;
    public SendReceiveMessageExample sr;

    /// <summary>What the player is currently touching, for the HUD's contextual prompt.</summary>
    public enum Interaction { None, Flower, Human, Mate, FemaleMosquito, Container }
    public Interaction CurrentInteraction { get; private set; }
    public WaterContainer CurrentContainer { get; private set; }
    /// <summary>A/X went down this frame (either hand).</summary>
    public bool PrimaryDown { get; private set; }

    private float interactionExpires;
    private bool primaryHeldLastFrame;

    private void Awake()
    {
        canmove = true;
        instance = this;
        rb = GetComponent<Rigidbody>();
        Current_Nec = Max_Nec/2;
    }

    void Update()
    {
        
        checkinput();
        if (Time.time > interactionExpires)
        {
            CurrentInteraction = Interaction.None;
            CurrentContainer = null;
        }
        // Frozen until the intro is dismissed and after the game ends.
        bool playing = GameManager.instance != null && GameManager.instance.IsPlaying;
        if (playing)
        {
            termalcam.SetActive(R_triggerValue);
            Move(L_moveInput);
            NectarUpdate();
        }
        else
        {
            termalcam.SetActive(false);
            rb.linearVelocity = Vector3.zero;
        }
    }

    /// <summary>Called from trigger callbacks every physics step; the context expires shortly after contact ends.</summary>
    public void ReportInteraction(Interaction kind, WaterContainer container = null)
    {
        CurrentInteraction = kind;
        CurrentContainer = container;
        interactionExpires = Time.time + 0.2f;
    }

    public void NectarUpdate()
    {
        if (Current_Nec > 0)
        {
            Current_Nec -= Time.deltaTime / Max_Nec;
        }
        else
        {
            GameManager.instance.GameOver(GameManager.GameOverReason.Starved);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        var container = other.gameObject.GetComponent<WaterContainer>();
        if (container != null)
        {
            ReportInteraction(Interaction.Container, container);
            // Eggs need standing water: containers only count once the rain has filled them.
            if (container.isFill && Current_Blood >= Max_Blood && isMate)
            {
                if (R_primaryValue)
                {
                    Current_Blood = 0;
                    EggLayed++;
                    GameManager.instance.SetScore(container.Score, Module1Text.EggsLaid);
                }
            }
        }
        var wild = other.gameObject.GetComponent<Wild_Mosquitos>();
        if (wild != null)
        {
            if (wild.Gender == Wild_Mosquitos.genderlist.male)
            {
                ReportInteraction(Interaction.Mate);
                if (!isMate && R_primaryValue)
                {
                    isMate = true;
                }
            }
            else
            {
                ReportInteraction(Interaction.FemaleMosquito);
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
        bool primaryHeld = R_primaryValue || L_primaryValue;
        PrimaryDown = primaryHeld && !primaryHeldLastFrame;
        primaryHeldLastFrame = primaryHeld;

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
            Vector3 moveDirection = (forward * direction.y + right * direction.x).normalized;
            moveDirection.y = R_moveInput.y * FlyUpSpeed;
            rb.linearVelocity = moveDirection * Speed;
            CamRot.transform.Rotate(0, R_moveInput.x * CamRotSpeed, 0);
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    public void Drink()
    {
        if (GameManager.instance.IsPlaying)
        {
            Current_Blood += Time.deltaTime;
            canmove = !R_primaryValue;
        }
    }
    public void DrinkNectar()
    {
        if (GameManager.instance.IsPlaying)
        {
            Current_Nec += Time.deltaTime;
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
