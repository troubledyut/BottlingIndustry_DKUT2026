using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text; // Explicitly resolves ambiguity with System.Net.Mime.MediaTypeNames.Text

public class StartupCheckScreen : MonoBehaviour
{
    public UnityPLCConnectBridge bridge;

    private Canvas _canvas;
    private Text[] _checkTexts = new Text[5];
    private Text _hintText;
    private Button _startButton;
    private bool _started = false;

    private void Awake()
    {
        if (bridge == null) bridge = FindObjectOfType<UnityPLCConnectBridge>();
        BuildUI();
        FreezeSimulation(true);
    }

    private void Update()
    {
        if (_started) return;

        bool check1 = bridge.serviceExeFound;
        bool check2 = bridge.serviceAlive;
        bool check3 = bridge.statusByte != 1;
        bool check4 = bridge.statusByte != 2;
        bool check5 = bridge.isConnected;

        UpdateCheckUI(0, check1, "1. UnityPLCConnect Executable Found", "Place UnityPLCConnect folder in application root");
        UpdateCheckUI(1, check2, "2. UnityPLCConnect Service Active", "Background service initializing...");
        UpdateCheckUI(2, check3, "3. Siemens PLCSIM Advanced Service Running", "Open Siemens PLCSIM Advanced Control Panel");
        UpdateCheckUI(3, check4, $"4. Virtual Instance Registered ('{bridge.instanceName}')", $"Create Virtual CPU '{bridge.instanceName}' in PLCSIM");
        UpdateCheckUI(4, check5, "5. Target CPU State: RUN", GetPlcStateHint(bridge.plcOperatingState));

        _startButton.interactable = check1 && check2 && check3 && check4 && check5;
    }

    private void UpdateCheckUI(int index, bool passed, string label, string failHint)
    {
        string mark = passed ? "<color=#00FF00>✔</color>" : "<color=#FF0000>✘</color>";
        _checkTexts[index].text = $"{mark} {label}";
        if (!passed && string.IsNullOrEmpty(_hintText.text))
        {
            _hintText.text = $"<b>Diagnostic Hint:</b> {failHint}";
        }
    }

    private string GetPlcStateHint(byte state)
    {
        switch (state)
        {
            case 0: return "Virtual CPU is powered off. Turn CPU power ON in PLCSIM Manager.";
            case 3: return "Virtual CPU is in STOP mode. Toggle CPU to RUN in TIA Portal or PLCSIM.";
            case 1: case 2: return "Virtual CPU starting up...";
            default: return "Verify TIA Portal project hardware configuration.";
        }
    }

    public void OnStartClicked()
    {
        _started = true;
        _canvas.enabled = false;
        FreezeSimulation(false);
        bridge.BeginSimulation();
    }

    private void FreezeSimulation(bool freeze)
    {
        Time.timeScale = freeze ? 0f : 1f;
    }

    private void BuildUI()
    {
        GameObject canvasObj = new GameObject("StartupCheckCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.12f, 0.96f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.2f, 0.15f);
        panelRt.anchorMax = new Vector2(0.8f, 0.85f);
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        for (int i = 0; i < 5; i++)
        {
            GameObject checkObj = new GameObject($"Check_{i}");
            checkObj.transform.SetParent(panel.transform, false);
            Text txt = checkObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 18;
            txt.supportRichText = true;
            RectTransform rt = checkObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.8f - (i * 0.11f));
            rt.anchorMax = new Vector2(0.92f, 0.88f - (i * 0.11f));
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _checkTexts[i] = txt;
        }

        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(panel.transform, false);
        _hintText = hintObj.AddComponent<Text>();
        _hintText.font = font;
        _hintText.fontSize = 15;
        _hintText.color = Color.yellow;
        _hintText.supportRichText = true;
        RectTransform hintRt = hintObj.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.08f, 0.22f);
        hintRt.anchorMax = new Vector2(0.92f, 0.30f);
        hintRt.offsetMin = hintRt.offsetMax = Vector2.zero;

        GameObject btnObj = new GameObject("StartButton");
        btnObj.transform.SetParent(panel.transform, false);
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.55f, 0.2f);
        _startButton = btnObj.AddComponent<Button>();
        _startButton.onClick.AddListener(OnStartClicked);
        RectTransform btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.3f, 0.06f);
        btnRt.anchorMax = new Vector2(0.7f, 0.18f);
        btnRt.offsetMin = btnRt.offsetMax = Vector2.zero;

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        Text btnTxt = btnTxtObj.AddComponent<Text>();
        btnTxt.font = font;
        btnTxt.fontSize = 18;
        btnTxt.text = "START SIMULATION";
        btnTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = btnTxtRt.offsetMax = Vector2.zero;
    }
}