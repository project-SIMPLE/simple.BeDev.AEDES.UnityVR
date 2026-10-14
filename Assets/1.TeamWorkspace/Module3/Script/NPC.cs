using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class NPC : MonoBehaviour
{
    public int BodyTmp;
    public bool Rash, Headache, BodyAche, StomachAche, Sick, Vormit, RedEyes, Tired;
    public string voiceLine;

    public bool roaming, Interacted;
    public Vector2 RoamingRange;
    public NavMeshAgent Agent;
    public Outline outline;
    public TextMeshProUGUI text;
    public M3_Player player;
    public enum Result
    {
        ToHospital, ToHome, Fine
    }
    public Result result;

    private void Awake()
    {
        outline = GetComponent <Outline>();
        outline.enabled = false;
        if(roaming)
        {
            StartCoroutine(Roaming());
        }
        SetResult();
        SetVoiceLine();
    }
    private void Start()
    {
        player = M3_Manager.instance.player;
    }
    public void SetResult()
    {
        if (Rash || Headache || BodyAche || StomachAche || Sick || Vormit || RedEyes || BodyTmp > 39)
        {
            result = Result.ToHospital;
            return;
        }
        else if (Tired)
        {
            result = Result.ToHome;
            return;
        }
        else
        {
            result = Result.Fine;
        }
    }
    public void SetVoiceLine()
    {
        string VL = null;
        VL = "I've been Having";
        if (Rash || Headache || BodyAche || StomachAche || Sick || Vormit || RedEyes || Tired)
        {
            if (Rash) VL += (" Rash,");
            if (Headache) VL += (" Headache,");
            if (BodyAche) VL += (" BodyAche,");
            if (StomachAche) VL += (" StomachAche,");
            if (Sick) VL += (" been feeling Sick,");
            if (Vormit) VL += (" Vormit a lot,");
            if (RedEyes) VL += (" RedEyes,");
            if (Tired) VL += (" Tired,");
        }
        else
        {
            VL = ("I'm Fine");
        }
        voiceLine = VL;
    }
    public IEnumerator Roaming()
    {
        Agent.SetDestination(new Vector3 (UnityEngine.Random.Range(RoamingRange.x*-1, RoamingRange.x), 0, UnityEngine.Random.Range(RoamingRange.y * -1, RoamingRange.y)));
        yield return new WaitUntil(() => Agent.remainingDistance<0.5f);
        StartCoroutine(Roaming());
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!Interacted)
        {
            if (other.tag == "Player")
            {
                if (player != this)
                {
                    outline.enabled = true;
                    player.Movement.moveSpeed = 0;
                    player.NPCInteracted(this);
                    player.ConversationUI.SetActive(true);
                    Invoke("LookatPlayer", 0);
                    StopCoroutine("Roaming");
                    Agent.isStopped = true;
                    Interacted = true;
                }
            }
        }
    }

    public void GetInstruction(Result r)
    {
        Agent.isStopped = false;
        switch (r)
        {
            case Result.ToHospital: Agent.SetDestination(M3_Manager.instance.Hospital.position); StartCoroutine(GoInside()); break;
            case Result.ToHome: Agent.SetDestination(M3_Manager.instance.House[UnityEngine.Random.Range(0, M3_Manager.instance.House.Length)].position); StartCoroutine(GoInside()); break;
            case Result.Fine: if(roaming) StartCoroutine(Roaming()); break;
        }
        if(r == result)
        {
            M3_Manager.instance.setScore(10);
        }
        else
        {
            M3_Manager.instance.setScore(-10);
        }
    }
    public IEnumerator GoInside()
    {
        yield return new WaitUntil(()=>Agent.remainingDistance<0.5f);
        Destroy(gameObject);
    }

    public IEnumerator LookatPlayer()
    {
        while (M3_Manager.instance.player.currentNPC == this) 
        {
            transform.LookAt(new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z));
        }
        yield return new WaitUntil(()=>player.currentNPC!= this);
        CancelInvoke("LookatPlayer");
    }
    public IEnumerator ShowText(string t)
    {
        string s = null;
        for (int i = 0; i < t.Length; i++)
        {
            s += voiceLine[i];
            text.text = s;
            yield return new WaitForSeconds(.1f);
        }

    }
}
