using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LanguageComponent : MonoBehaviour
{
    public string m_languageID = "";

    public Text m_text;

    public void Init()
    {
        SetLanguage();
    }

    public void SetLanguage()
    {
        m_text.text = LanguageManager.GetContent(m_languageID);
    }
}
