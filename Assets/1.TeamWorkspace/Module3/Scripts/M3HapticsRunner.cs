using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Holds the success beats (the second lands after a gap, so something must count time) and
/// the last pulse sent, which the playtest harness reads: there is no way to feel a buzz there.
/// </summary>
public class M3HapticsRunner : MonoBehaviour
{
    [Header("Last pulse, for checking without a headset")]
    public string lastPulse = "";
    public string lastHand = "";
    public float lastAmplitude;
    public float lastSeconds;
    public int pulses;
    [Tooltip("Log every pulse to the console.")]
    public bool logPulses;

    private Component beatHand;
    private float firstBeatAt = -1f, secondBeatAt = -1f;

    public void Record(Component hand, float amplitude, float seconds, string what)
    {
        lastPulse = what;
        lastHand = hand != null ? hand.name : "(none: mouse or keyboard)";
        lastAmplitude = amplitude;
        lastSeconds = seconds;
        pulses++;
        if (logPulses) Debug.Log($"Module 3 haptics: {what} {amplitude:0.00} x {seconds:0.000}s on {lastHand}" + (M3Haptics.Enabled ? "" : " (off)"));
    }

    private bool subscribed;

    private void Update()
    {
        if (!subscribed && M3Session.Instance != null)
        {
            M3Session.Instance.OnActionResolved += OnAction;
            subscribed = true;
        }

        float now = Time.time;
        if (firstBeatAt >= 0f && now >= firstBeatAt)
        {
            firstBeatAt = -1f;
            M3Haptics.Pulse(beatHand, M3Haptics.SuccessAmplitude, M3Haptics.SuccessSeconds, "success 1/2");
        }
        if (secondBeatAt >= 0f && now >= secondBeatAt)
        {
            secondBeatAt = -1f;
            M3Haptics.Pulse(beatHand, M3Haptics.SuccessAmplitude, M3Haptics.SuccessSeconds, "success 2/2");
        }
    }

    private void OnAction(ActionResult r)
    {
        // Went through, in whatever way. A referral that was not needed still happened and is
        // "gently corrected, not punished" (section 10), so it gets the same beat as any other.
        if (r.Outcome == ActionOutcome.Invalid || r.Outcome == ActionOutcome.AlreadyDone
            || r.Outcome == ActionOutcome.NoNetsRemaining) return;
        beatHand = M3Haptics.TakePressingHand();
        float now = Time.time;
        firstBeatAt = now + M3Haptics.SuccessFirstBeat;
        secondBeatAt = now + M3Haptics.SuccessSecondBeat;
    }

    private void OnDestroy()
    {
        if (subscribed && M3Session.Instance != null) M3Session.Instance.OnActionResolved -= OnAction;
        M3Haptics.Forget(this);
    }
}
