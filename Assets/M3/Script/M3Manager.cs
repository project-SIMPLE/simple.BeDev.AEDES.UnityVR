using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class M3Manager : MonoBehaviour
{
    public Transform HospitalPos;
    public static M3Manager instance;

    private void Awake()
    {
        Human[] human;
        instance = this;

    }
}
