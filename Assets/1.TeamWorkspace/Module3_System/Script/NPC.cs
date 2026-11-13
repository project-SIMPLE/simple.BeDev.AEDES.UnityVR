using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class NPC : MonoBehaviour
{

    public bool  Interacted;
  
 
    private void OnTriggerEnter(Collider other)
    {
        if (!Interacted)
        {
            if (other.tag == "Player")
            {
                Interacted = true;
            }
        }
    }
  
}
