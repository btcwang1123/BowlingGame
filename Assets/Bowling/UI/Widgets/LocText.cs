using TMPro;
using UnityEngine;

// Attach to any TMP text object; set `key` to show localized text that refreshes live when
// the language changes.
[RequireComponent(typeof(TMP_Text))]
public class LocText : MonoBehaviour
{
    [SerializeField] private string key;

    TMP_Text text;

    void Awake() => text = GetComponent<TMP_Text>();

    void OnEnable()
    {
        Loc.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable() => Loc.LanguageChanged -= Refresh;

    void Refresh() => text.text = Loc.T(key);
}
