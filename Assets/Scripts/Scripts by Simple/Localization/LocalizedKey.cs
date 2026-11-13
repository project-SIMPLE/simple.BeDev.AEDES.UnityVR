using UnityEngine;
using TMPro;

/// <summary>
/// Drives a TMP label (and optionally a voice-over clip) from a localization key.
/// Supersedes <see cref="LocalizedText"/>: it accepts world-space TextMeshPro as well as
/// TextMeshProUGUI, which Module 3 needs for the in-world household panels.
/// </summary>
public class LocalizedKey : MonoBehaviour
{
    public string localizationKey;
    public AudioSource audioSource;
    public TMP_Text textComponent;

    private void Awake()
    {
        if (textComponent == null) textComponent = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        UpdateText();
        LocalizationManager.OnLanguageChanged += UpdateText;

        if (audioSource != null)
        {
            UpdateAudioClip();
            LocalizationManager.OnLanguageChanged += UpdateAudioClip;
        }
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= UpdateText;
        if (audioSource != null)
        {
            LocalizationManager.OnLanguageChanged -= UpdateAudioClip;
        }
    }

    /// <summary>Swaps the key at runtime, e.g. when a villager's dialogue line changes.</summary>
    public void SetKey(string key)
    {
        localizationKey = key;
        UpdateText();
        if (audioSource != null) UpdateAudioClip();
    }

    public void UpdateText()
    {
        if (string.IsNullOrEmpty(localizationKey)) return;

        if (textComponent == null)
        {
            Debug.LogWarning("No TMP_Text component found on " + gameObject.name);
            return;
        }

        string localizedText = LocalizationManager.Instance.GetLocalizedValue(localizationKey);
        if (!string.IsNullOrEmpty(localizedText))
        {
            textComponent.text = localizedText;
        }
    }

    public void UpdateAudioClip()
    {
        if (string.IsNullOrEmpty(localizationKey)) return;

        string currentLanguage = LocalizationManager.Instance.GetLanguage();
        string audioClipPath = $"Localization/Audio/{currentLanguage}/{localizationKey}";

        AudioClip loadedClip = Resources.Load<AudioClip>(audioClipPath);

        if (loadedClip != null)
        {
            audioSource.clip = loadedClip;
        }
        else
        {
            Debug.LogWarning($"AudioClip not found at path: '{audioClipPath}'");
        }
    }
}
