### Localization System for SIMPLE Unity template

1. **Language data** — edit `Assets/Resources/Localization/LocalizationData.csv`. The first column
   is the key, every other column is a language named in the header row.
   Values **may** contain commas, line breaks and quotes, as long as the value is wrapped in
   double quotes and a literal quote is written as `""`:

   ```csv
   Key,English,Lao
   villager.fever3days,"She has been hot for three days, since Monday.",...
   ```

   A row whose key starts with `#` is treated as a comment and skipped.

2. **Make a text switch language** — add `LocalizedKey` to the GameObject carrying a `TMP_Text`
   (either `TextMeshProUGUI` or a world-space `TextMeshPro`) and set the key. Optionally assign an
   `AudioSource` and the component will also load
   `Resources/Localization/Audio/<Language>/<key>` as voice-over.
   `LocalizedText` is the older UGUI-only equivalent and is kept for existing scenes.

3. **Change language** — `LocalizationManager.Instance.SetLanguage("Lao");`. The choice is
   remembered in `PlayerPrefs["Language"]`. `GetLanguages()` enumerates what the CSV offers.

4. **Runtime substitution** — `GetLocalizedValue("key", arg0, arg1)` runs `string.Format` over the
   value, for lines like `"she has been hot for {0} days"`.

5. **Auto-colouring** — `Resources/Localization/ColorTexts.asset` lists words that should always be
   tinted (warning signs, for instance). They are wrapped in `<color=#RRGGBB>` at load time.

6. **Fonts** — Lao renders through
   `Assets/1.TeamWorkspace/Team Assets/TextMesh Pro/Fonts/Lao/Lao_SomVang SDF_Custom.asset`.
   It is a **dynamic** atlas backed by `Lao_SomVang.ttf`, so any Lao or Latin glyph is rasterised
   on demand rather than being limited to a pre-baked set.

Make sure a `LocalizationManager` (it is `DontDestroyOnLoad`) exists in the first scene.
