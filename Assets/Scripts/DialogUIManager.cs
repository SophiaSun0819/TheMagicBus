using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DialogUIManager : MonoBehaviour
{
    public float clickRayDistance = 100f;

    private NPCCharacter selectedNPC = null;
    private bool isWaiting = false;
    private List<string> chatHistory = new List<string>();

    // 本次小故事的三段内容
    private List<string> storySegments = new List<string>();
    private int currentSegmentIndex = -1;
    private bool storyRequested = false;
    private bool storyReady = false;

    // 可以在场景中预先放好一个 Canvas（例如命名为 DialogCanvas_3D）并拖到这里
    public GameObject dialogCanvas;
    [Header("Red Blood Cell (另一套对话)")]
    [Tooltip("Red Blood Cell 用的 Canvas，与上面功能相同")]
    public GameObject redBloodCellCanvas;
    /// <summary> 当前正在使用的 Canvas（白细胞或红细胞），用于关闭、射线检测等 </summary>
    private GameObject currentDialogCanvas;
    /// <summary> 只隐藏/显示 Panel，不关整个 Canvas，避免 XR TrackedDeviceGraphicRaycaster.OnDisable 报 KeyNotFoundException </summary>
    private GameObject dialogPanel;
    private Text chatText;
    private Text npcNameText;
    private Text statusText;
    private ScrollRect scrollRect;
    private Button nextButton;
    private Text nextButtonLabel;
    private GameObject talkButtonGO;
    private Button talkButton;
    private Text talkButtonLabel;

    // 语音问答：录音状态与用到的 clip（Microphone.Start 返回的）
    private bool isRecording = false;
    private float recordingStartTime = 0f;
    private AudioClip recordedClip = null;
    private string recordingDeviceName = null;
    private const int RECORD_SECONDS = 10;
    private const int RECORD_FREQUENCY = 16000;
    private static readonly string VOICE_SYSTEM_PROMPT = "You are a science teacher for 8-12 year olds, explaining the human body in a simple, fun way. Answer in one or two short sentences in plain English. No complex terms.";
    /// <summary> Story page: fixed science-teacher persona, English only. </summary>
    private static readonly string STORY_SYSTEM_PROMPT = "You are a science teacher for 8-12 year olds. Use simple, fun English. No complex medical terms.";

    [Header("Story (3 segments, editable in Inspector)")]
    [Tooltip("第一段")]
    public string storySegment1 = "Tiny germs have sneaked in through the wound! But don't worry—our body has a group of brave guards called white blood cells. They rush over, surround the germs, and \"swallow\" them up, just like heroes defeating little monsters!";
    [Tooltip("第二段")]
    public string storySegment2 = "Oh no! Germs have broken into the body! White blood cells are like little police officers. As soon as they spot the bad germs, they chase after them. They catch the germs and destroy them, protecting our body from getting sick.";
    [Tooltip("第三段")]
    public string storySegment3 = "Germs have entered through the wound, but the white blood cells have already noticed them! The white blood cells chase the germs, surround them, and slowly break them down. This way, the germs can't keep causing trouble anymore!";

    [Header("Red Blood Cell 故事 (三段)")]
    public string redBloodCellStory1 = "Red blood cells are like tiny delivery trucks inside your body! They carry oxygen from your lungs to every part of you.";
    public string redBloodCellStory2 = "When you breathe in, oxygen goes into your blood. Red blood cells grab the oxygen and travel through your blood vessels to bring it to your muscles, brain, and everywhere else.";
    public string redBloodCellStory3 = "After they drop off the oxygen, red blood cells pick up something called carbon dioxide and bring it back to the lungs so you can breathe it out. They work all day, every day!";

    /// <summary> 防连点：下一页按钮点击后在此时间之前不再响应 </summary>
    private float nextButtonCooldownUntil = 0f;
    private const float NEXT_BUTTON_COOLDOWN = 0.5f;

    /// <summary> 聊天记录最多保留条数，避免对话多了后界面卡顿或出现遮罩异常 </summary>
    private const int MAX_CHAT_HISTORY_LINES = 40;

    /// <summary> 玩家是否已经开始语音问答：一旦开始就只显示对话，不再显示故事内容，方便持续多轮对话 </summary>
    private bool hasStartedVoiceQA = false;

    void Start()
    {
        if (dialogCanvas == null)
        {
            Debug.LogWarning("[DialogUIManager] No dialogCanvas assigned.");
            return;
        }
        BuildUI(dialogCanvas);
        if (dialogPanel != null) dialogPanel.SetActive(false);
        if (dialogCanvas != null) dialogCanvas.SetActive(false);
        // 不默认关 redBloodCellCanvas，保持场景里原有状态
        // 游戏开始时自动与第一个NPC对话
        NPCCharacter npc = FindFirstObjectByType<NPCCharacter>();
        if (npc != null) SelectNPC(npc);
    }

    void BuildUI(GameObject targetCanvas)
    {
        if (targetCanvas == null) return;

        // 切换 Canvas 时先清空引用，避免继续指向另一个 Canvas 的控件，导致三段话/翻页不同步
        dialogPanel = null;
        chatText = null;
        npcNameText = null;
        statusText = null;
        scrollRect = null;
        nextButton = null;
        nextButtonLabel = null;
        talkButtonGO = null;
        talkButton = null;
        talkButtonLabel = null;

        Canvas canvas = targetCanvas.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = targetCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
        }

        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = targetCanvas.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
        }

        GraphicRaycaster raycaster = targetCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = targetCanvas.AddComponent<GraphicRaycaster>();

        RectTransform cRT = targetCanvas.GetComponent<RectTransform>();
        if (cRT == null)
            cRT = targetCanvas.AddComponent<RectTransform>();
        cRT.sizeDelta = new Vector2(300, 210);
        cRT.localScale = Vector3.one * 0.01f;

        // 场景里已有 Panel 时直接复用
        Transform existingPanel = targetCanvas.transform.Find("Panel");
        if (existingPanel != null)
        {
            dialogPanel = existingPanel.gameObject;
            BindExistingUI(dialogPanel);
            currentDialogCanvas = targetCanvas;
            return;
        }

        // 外边框
        MakeImage(targetCanvas.transform, "Border", new Color(0.3f, 0.6f, 1f, 0.85f), Vector2.zero, new Vector2(304, 214));

        // 主背景
        dialogPanel = MakeImage(targetCanvas.transform, "Panel", new Color(0.04f, 0.07f, 0.14f, 0.97f), Vector2.zero, new Vector2(300, 210));
        GameObject panel = dialogPanel;

        // 标题栏
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(panel.transform, false);
        titleBar.AddComponent<Image>().color = new Color(0.09f, 0.17f, 0.42f, 1f);
        RectTransform tbRT = titleBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.pivot = new Vector2(0.5f, 1f);
        tbRT.offsetMin = new Vector2(0, -34); tbRT.offsetMax = new Vector2(0, 0);

        npcNameText = MakeText(titleBar.transform, "NPCName", "NPC",
            new Color(0.4f, 0.85f, 1f), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-4, -2));

        // 关闭按钮
        GameObject closeGO = MakeButton(panel.transform, "CloseBtn", "X", new Color(0.55f, 0.1f, 0.1f, 1f));
        RectTransform closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 1); closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot = new Vector2(1, 1);
        closeRT.anchoredPosition = new Vector2(-2, -2);
        closeRT.sizeDelta = new Vector2(28, 28);
        closeGO.GetComponent<Button>().onClick.AddListener(CloseDialog);

        // 滚动聊天区
        GameObject scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(panel.transform, false);
        scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        scrollRect = scrollGO.AddComponent<ScrollRect>();
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(6, 44); scrollRT.offsetMax = new Vector2(-6, -36);

        GameObject vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        vpGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        vpGO.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        chatText = contentGO.AddComponent<Text>();
        chatText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        chatText.fontSize = 12; chatText.color = Color.white;
        chatText.alignment = TextAnchor.LowerLeft;
        chatText.resizeTextForBestFit = false;
        chatText.supportRichText = true;
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0, 0);
        contentRT.offsetMin = new Vector2(4, 4); contentRT.offsetMax = new Vector2(-4, -4);

        scrollRect.content = contentRT;
        scrollRect.viewport = vpRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // 状态文字
        statusText = MakeText(panel.transform, "Status", "",
            new Color(1f, 0.78f, 0.2f), 11, FontStyle.Italic, TextAnchor.MiddleLeft,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(6, 34), new Vector2(-6, 50));

        // 底部一行两键：左“下一页/关闭”，右“对话”（最后一页时才显示）
        // “下一页”/“关闭”按钮（左侧，最后一页时缩为左半宽）
        GameObject nextGO = MakeButton(panel.transform, "NextBtn", "Next", new Color(0.15f, 0.45f, 0.85f, 1f));
        RectTransform nextRT = nextGO.GetComponent<RectTransform>();
        nextRT.anchorMin = new Vector2(0, 0); nextRT.anchorMax = new Vector2(1, 0);
        nextRT.pivot = new Vector2(0.5f, 0);
        nextRT.anchoredPosition = new Vector2(0, 8);
        nextRT.offsetMin = new Vector2(6, 8); nextRT.offsetMax = new Vector2(-6, 36);
        nextButtonLabel = nextGO.GetComponentInChildren<Text>();
        if (nextButtonLabel != null) nextButtonLabel.fontSize = 12;
        nextButton = nextGO.GetComponent<Button>();
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextButtonClicked);
            nextButton.interactable = false;
        }

        // “对话”按钮（关闭的右边，仅最后一页显示）
        talkButtonGO = MakeButton(panel.transform, "TalkBtn", "Talk", new Color(0.2f, 0.6f, 0.3f, 1f));
        RectTransform talkRT = talkButtonGO.GetComponent<RectTransform>();
        talkRT.anchorMin = new Vector2(0.5f, 0); talkRT.anchorMax = new Vector2(1f, 0);
        talkRT.pivot = new Vector2(0.5f, 0);
        talkRT.anchoredPosition = new Vector2(0, 8);
        talkRT.offsetMin = new Vector2(4, 8); talkRT.offsetMax = new Vector2(-6, 36);
        talkButtonLabel = talkButtonGO.GetComponentInChildren<Text>();
        if (talkButtonLabel != null) talkButtonLabel.fontSize = 12;
        talkButton = talkButtonGO.GetComponent<Button>();
        if (talkButton != null)
            talkButton.onClick.AddListener(OnTalkButtonClicked);
        talkButtonGO.SetActive(false);
        currentDialogCanvas = targetCanvas;
    }

    /// <summary> 复用场景里已有的 Panel（DialogCanvas_3D/Panel）：只绑定引用和事件，不创建新 Panel/ScrollView；缺少 TalkBtn 时在 Panel 内创建一个。</summary>
    void BindExistingUI(GameObject panel)
    {
        Transform root = panel.transform;

        Transform sv = root.Find("ScrollView");
        if (sv != null)
        {
            scrollRect = sv.GetComponent<ScrollRect>();
            Transform vp = sv.Find("Viewport");
            if (vp != null)
            {
                Transform content = vp.Find("Content");
                if (content != null)
                {
                    chatText = content.GetComponent<Text>();
                    if (chatText == null) chatText = content.GetComponentInChildren<Text>();
                }
            }
        }

        Transform tb = root.Find("TitleBar");
        if (tb != null)
        {
            Transform nn = tb.Find("NPCName");
            if (nn != null)
                npcNameText = nn.GetComponent<Text>();
            if (npcNameText == null && tb != null)
                npcNameText = tb.GetComponentInChildren<Text>();
        }

        Transform st = root.Find("Status");
        if (st != null) statusText = st.GetComponent<Text>();

        Transform next = root.Find("NextBtn");
        if (next != null)
        {
            nextButton = next.GetComponent<Button>();
            nextButtonLabel = next.GetComponentInChildren<Text>();
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(OnNextButtonClicked);
                nextButton.interactable = false;
            }
        }

        Transform close = root.Find("CloseBtn");
        if (close != null)
        {
            Button b = close.GetComponent<Button>();
            if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(CloseDialog); }
        }

        Transform talk = root.Find("TalkBtn");
        if (talk != null)
        {
            talkButtonGO = talk.gameObject;
            talkButton = talk.GetComponent<Button>();
            talkButtonLabel = talk.GetComponentInChildren<Text>();
            if (talkButton != null) { talkButton.onClick.RemoveAllListeners(); talkButton.onClick.AddListener(OnTalkButtonClicked); }
            talkButtonGO.SetActive(false);
        }
        else
        {
            // 原有结构没有 Talk 按钮，在 Panel 内直接加一个（不新建 Panel）
            talkButtonGO = MakeButton(root, "TalkBtn", "Talk", new Color(0.2f, 0.6f, 0.3f, 1f));
            RectTransform talkRT = talkButtonGO.GetComponent<RectTransform>();
            talkRT.anchorMin = new Vector2(0.5f, 0);
            talkRT.anchorMax = new Vector2(1f, 0);
            talkRT.pivot = new Vector2(0.5f, 0);
            talkRT.anchoredPosition = new Vector2(0, 8);
            talkRT.offsetMin = new Vector2(4, 8);
            talkRT.offsetMax = new Vector2(-6, 36);
            talkButtonLabel = talkButtonGO.GetComponentInChildren<Text>();
            if (talkButtonLabel != null) talkButtonLabel.fontSize = 12;
            talkButton = talkButtonGO.GetComponent<Button>();
            if (talkButton != null) talkButton.onClick.AddListener(OnTalkButtonClicked);
            talkButtonGO.SetActive(false);
        }
    }

    static GameObject MakeImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return go;
    }

    static Text MakeText(Transform parent, string name, string text, Color color,
        int size, FontStyle style, TextAnchor align,
        Vector2 aMin, Vector2 aMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text; t.color = color; t.fontSize = size;
        t.fontStyle = style; t.alignment = align; t.supportRichText = true;
        t.resizeTextForBestFit = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        return t;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>(); img.color = color;
        Button btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = color * 1.3f; cb.pressedColor = color * 0.6f; btn.colors = cb;
        // 不加 RectTransform：AddComponent<Image> 后 Unity 已自动挂上 RectTransform，再加会报重复
        GameObject lblGO = new GameObject("Lbl");
        lblGO.transform.SetParent(go.transform, false);
        Text t = lblGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = label; t.color = Color.white; t.fontSize = 13;
        t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
        RectTransform lRT = lblGO.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        return go;
    }

    void Update()
    {
        // 只有当前没在跟该 NPC 对话时，点中 NPC 才打开/切换对话，避免已打开时误触 NPC 又重开故事页
        if (Input.GetMouseButtonDown(0) && !IsPointerOverDialog())
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, clickRayDistance))
            {
                NPCCharacter npc = hit.collider.GetComponent<NPCCharacter>();
                if (npc != null && (selectedNPC == null || selectedNPC != npc))
                    SelectNPC(npc);
            }
        }

        // Quest3 / 手柄：Submit 仅在前几页触发“下一页”，最后一页不触发，避免误关
        if (selectedNPC != null && dialogPanel != null && dialogPanel.activeSelf)
        {
            if (Input.GetButtonDown("Submit") && (storySegments.Count == 0 || currentSegmentIndex < storySegments.Count - 1))
                OnNextButtonClicked();
        }

        if (Input.GetKeyDown(KeyCode.Escape)) CloseDialog();

        // 录音时显示 10 秒倒计时
        if (isRecording && statusText != null)
        {
            float elapsed = Time.time - recordingStartTime;
            int left = Mathf.Max(0, Mathf.CeilToInt(RECORD_SECONDS - elapsed));
            statusText.text = left > 0 ? "Recording: " + left + " s left" : "Recording: 0 s";
        }
    }

    bool IsPointerOverDialog()
    {
        if (!dialogCanvas || !dialogPanel || !dialogPanel.activeSelf) return false;
        RectTransform rt = dialogCanvas.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 smin = Camera.main.WorldToScreenPoint(corners[0]);
        Vector2 smax = smin;
        for (int i = 1; i < 4; i++)
        {
            Vector2 sp = Camera.main.WorldToScreenPoint(corners[i]);
            smin = Vector2.Min(smin, sp); smax = Vector2.Max(smax, sp);
        }
        Vector2 m = Input.mousePosition;
        return m.x >= smin.x && m.x <= smax.x && m.y >= smin.y && m.y <= smax.y;
    }

    void SelectNPC(NPCCharacter npc)
    {
        selectedNPC = npc;
        hasStartedVoiceQA = false;
        chatHistory.Clear();
        storySegments.Clear();
        currentSegmentIndex = -1;
        storyRequested = false;
        storyReady = false;

        // 根据 NPC 名字选 Canvas 和故事：red blood cell 用 redBloodCellCanvas + 红细胞故事，否则用 dialogCanvas + 白细胞故事
        bool useRedBloodCell = redBloodCellCanvas != null && IsRedBloodCellNPC(npc.characterName);
        GameObject canvasToUse = useRedBloodCell ? redBloodCellCanvas : dialogCanvas;
        if (canvasToUse == null) canvasToUse = dialogCanvas;

        // 只关掉“当前不用的”那个，不关 red blood cell canvas（保持场景里原有状态）
        if (canvasToUse == redBloodCellCanvas && dialogCanvas != null)
            dialogCanvas.SetActive(false);
        BuildUI(canvasToUse);
        currentDialogCanvas = canvasToUse;

        if (npcNameText != null) npcNameText.text = "  " + npc.characterName;
        if (statusText != null) statusText.text = "Preparing story...";
        if (nextButton != null) nextButton.interactable = false;
        if (nextButtonLabel != null) nextButtonLabel.text = "Loading...";
        if (talkButtonGO != null) talkButtonGO.SetActive(false);
        RectTransform nextRT = nextButton != null ? nextButton.GetComponent<RectTransform>() : null;
        if (nextRT != null)
        {
            nextRT.anchorMin = new Vector2(0, 0);
            nextRT.anchorMax = new Vector2(1, 0);
            nextRT.offsetMin = new Vector2(6, 8);
            nextRT.offsetMax = new Vector2(-6, 36);
        }
        RefreshChat();
        if (currentDialogCanvas != null) currentDialogCanvas.SetActive(true);
        if (dialogPanel != null) dialogPanel.SetActive(true);

        RequestFullStoryFromAI();
    }

    void CloseDialog()
    {
        if (selectedNPC != null) selectedNPC.SetTalking(false);
        selectedNPC = null;
        if (dialogPanel != null) dialogPanel.SetActive(false);
        if (currentDialogCanvas != null) currentDialogCanvas.SetActive(false);
        chatHistory.Clear();
    }

    /// <summary>
    /// 使用固定的三段故事，不再请求 AI
    /// </summary>
    void RequestFullStoryFromAI()
    {
        if (selectedNPC == null || storyRequested) return;

        storyRequested = true;
        isWaiting = false;
        storyReady = false;
        selectedNPC.SetTalking(false);

        storySegments.Clear();
        bool useRed = IsRedBloodCellNPC(selectedNPC.characterName);
        if (useRed)
        {
            if (!string.IsNullOrEmpty(redBloodCellStory1)) storySegments.Add(redBloodCellStory1);
            if (!string.IsNullOrEmpty(redBloodCellStory2)) storySegments.Add(redBloodCellStory2);
            if (!string.IsNullOrEmpty(redBloodCellStory3)) storySegments.Add(redBloodCellStory3);
        }
        else
        {
            if (!string.IsNullOrEmpty(storySegment1)) storySegments.Add(storySegment1);
            if (!string.IsNullOrEmpty(storySegment2)) storySegments.Add(storySegment2);
            if (!string.IsNullOrEmpty(storySegment3)) storySegments.Add(storySegment3);
        }

        storyReady = true;
        currentSegmentIndex = -1;
        if (statusText != null) statusText.text = "Click Next to hear the story.";

        if (nextButton != null)
            nextButton.interactable = true;

        if (nextButtonLabel != null)
            nextButtonLabel.text = "Next";

        // 打开对话后直接显示第一段，避免“没有显示故事”
        if (storySegments.Count > 0)
        {
            chatHistory.Clear();
            chatHistory.Add(storySegments[0]);
            currentSegmentIndex = 0;
            if (storySegments.Count == 1)
            {
                if (nextButtonLabel != null) nextButtonLabel.text = "Close";
                if (nextButton != null) { nextButton.onClick.RemoveAllListeners(); nextButton.onClick.AddListener(CloseDialog); }
                if (talkButtonGO != null) talkButtonGO.SetActive(true);
            }
        }
        RefreshChat();
    }

    static bool IsRedBloodCellNPC(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return false;
        string n = System.Text.RegularExpressions.Regex.Replace(characterName.Trim(), @"\s+", " ");
        return n.Equals("red blood cell", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 玩家点击“下一页”按钮或手柄按键时调用
    /// </summary>
    void OnNextButtonClicked()
    {
        if (!storyReady)
            return;
        // 防连点：冷却期内忽略
        if (Time.unscaledTime < nextButtonCooldownUntil)
            return;
        nextButtonCooldownUntil = Time.unscaledTime + NEXT_BUTTON_COOLDOWN;
        if (nextButton != null)
            nextButton.interactable = false;
        Invoke(nameof(ReenableNextButton), NEXT_BUTTON_COOLDOWN);

        // 还有下一段没展示
        if (currentSegmentIndex + 1 < storySegments.Count)
        {
            currentSegmentIndex++;

            // 每次只显示当前这一段，让孩子专注看这一页
            chatHistory.Clear();
            chatHistory.Add(storySegments[currentSegmentIndex]);
            RefreshChat();

            // Last segment: 只显示 Close + Talk，不自动关；只有用户点 Close 或 X 才关
            if (currentSegmentIndex == storySegments.Count - 1)
            {
                if (nextButtonLabel != null) nextButtonLabel.text = "Close";
                RectTransform nextRT = nextButton != null ? nextButton.GetComponent<RectTransform>() : null;
                if (nextRT != null)
                {
                    nextRT.anchorMin = new Vector2(0, 0);
                    nextRT.anchorMax = new Vector2(0.5f, 0);
                    nextRT.offsetMin = new Vector2(6, 8);
                    nextRT.offsetMax = new Vector2(-4, 36);
                }
                // 最后一页时该按钮改为“关闭”：只响应 Close，不再翻页
                if (nextButton != null)
                {
                    nextButton.onClick.RemoveAllListeners();
                    nextButton.onClick.AddListener(CloseDialog);
                }
                if (talkButtonGO != null) talkButtonGO.SetActive(true);
                if (talkButtonLabel != null) talkButtonLabel.text = "Talk";
                if (statusText != null) statusText.text = "Ask white blood cell anything";
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    void ReenableNextButton()
    {
        if (nextButton != null && storyReady && dialogPanel != null && dialogPanel.activeSelf)
            nextButton.interactable = true;
    }

    void SetBottomCloseButtonInteractable(bool interactable)
    {
        if (nextButton == null || nextButtonLabel == null) return;
        if (nextButtonLabel.text == "Close")
            nextButton.interactable = interactable;
    }

    /// <summary>
    /// “对话”按钮：第一次点击开始录音，第二次点击结束并语音转文字 → 显示在 canvas → 发给 AI 并用同样语气简短回答
    /// </summary>
    void OnTalkButtonClicked()
    {
        if (selectedNPC == null || isWaiting) return;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (isRecording)
        {
            // 结束录音 → 转文字 → 显示 → 发 AI
            StopRecordingAndSend();
            return;
        }

        // 固定使用获取到的麦克风列表里的第一个设备
        string[] devices = Microphone.devices;
        recordingDeviceName = (devices != null && devices.Length > 0) ? devices[0] : null;
        recordedClip = Microphone.Start(recordingDeviceName, false, RECORD_SECONDS, RECORD_FREQUENCY);
        if (recordedClip == null)
        {
            statusText.text = "Microphone failed. Check permissions.";
            Debug.LogWarning("[Voice] Microphone.Start 失败，recordedClip 为 null");
            return;
        }

        Debug.Log("[Voice] 使用麦克风(列表第一个): " + (string.IsNullOrEmpty(recordingDeviceName) ? "default" : recordingDeviceName));
        Debug.Log("[Voice] Start recording, length (sec): " + RECORD_SECONDS + ", freq: " + RECORD_FREQUENCY);

        recordingStartTime = Time.time;
        isRecording = true;
        SetBottomCloseButtonInteractable(false);
        if (talkButtonLabel != null) talkButtonLabel.text = "End";
        if (statusText != null) statusText.text = "Recording: " + RECORD_SECONDS + " s left";
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void StopRecordingAndSend()
    {
        if (!isRecording || recordedClip == null)
        {
            isRecording = false;
            if (talkButtonLabel != null) talkButtonLabel.text = "Talk";
            SetBottomCloseButtonInteractable(true);
            return;
        }
        // Clear selection when stopping recording
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // 先取“当前录到的样本数”，再 End，这样才知道实际说了多长（否则整段都是 10 秒）
        int recordedSamples = Microphone.GetPosition(recordingDeviceName);
        Microphone.End(recordingDeviceName);
        AudioClip fullClip = recordedClip;
        recordedClip = null;
        isRecording = false;
        if (talkButtonLabel != null) talkButtonLabel.text = "Talk";
        if (statusText != null) statusText.text = "Recognizing speech...";
        SetBottomCloseButtonInteractable(false);

        float recordedSeconds = fullClip != null && fullClip.frequency > 0
            ? (recordedSamples / (float)fullClip.frequency) : 0f;

        // 打印实际录音时长（你说了多久就显示多久，不再固定 10 秒）
        Debug.Log("[Voice] Stop recording. 实际录音: " + recordedSeconds.ToString("F1") + "s (最大 " + RECORD_SECONDS + "s), channels: " +
                  (fullClip != null ? fullClip.channels : 0) + ", freq: " + (fullClip != null ? fullClip.frequency : 0));

        // 说话太短就不发识别，避免静音或误触
        if (recordedSeconds < 0.5f)
        {
            statusText.text = "Too short. Speak clearly then tap End.";
            Debug.LogWarning("[Voice] 录音过短 (" + recordedSeconds.ToString("F1") + "s)，未发送识别");
            SetBottomCloseButtonInteractable(true);
            return;
        }

        // 只取“实际录到的这一段”发给 Whisper，不再发整段 10 秒（减少静音、识别更准）
        AudioClip clipToSend = TrimClipToRecordedLength(fullClip, recordedSamples);
        if (clipToSend == null)
            clipToSend = fullClip;

        byte[] wav = AudioClipToWav(clipToSend);
        if (wav == null || wav.Length == 0)
        {
            statusText.text = "Recording failed.";
            Debug.LogWarning("[Voice] AudioClipToWav 失败，wav 为空或长度为 0");
            SetBottomCloseButtonInteractable(true);
            return;
        }

        if (OpenAIManager.Instance == null)
        {
            chatHistory.Add("<color=red>[Error] OpenAIManager not found!</color>");
            RefreshChat();
            statusText.text = "";
            SetBottomCloseButtonInteractable(true);
            return;
        }

        isWaiting = true;
        OpenAIManager.Instance.TranscribeAudio(wav, (spokenText) =>
        {
            // 这里打印一次识别结果（包括失败的情况），方便在 Log 里确认“是否成功读取语音”
            if (string.IsNullOrWhiteSpace(spokenText))
            {
                Debug.LogWarning("[Voice] Whisper 识别失败或返回空文本。");
            }
            else
            {
                Debug.Log("[Voice] Whisper 识别文本: " + spokenText);
            }

            if (string.IsNullOrWhiteSpace(spokenText))
            {
                isWaiting = false;
                statusText.text = "Couldn't hear. Try again.";
                SetBottomCloseButtonInteractable(true);
                return;
            }

            // First voice question: switch to conversation-only view
            if (!hasStartedVoiceQA)
                hasStartedVoiceQA = true;
            chatHistory.Add("<color=#aaffaa>You: </color> " + spokenText);
            RefreshChat();
            statusText.text = selectedNPC != null ? selectedNPC.characterName + " is thinking..." : "Thinking...";

            OpenAIManager.Instance.SendMessageWithPrompt(VOICE_SYSTEM_PROMPT, spokenText, (reply) =>
            {
                isWaiting = false;
                statusText.text = "";
                SetBottomCloseButtonInteractable(true);
                if (selectedNPC != null)
                    chatHistory.Add("<color=#ffdd66>" + selectedNPC.characterName + ": </color> " + reply);
                else
                    chatHistory.Add("<color=#ffdd66>Reply: </color> " + reply);
                TrimChatHistoryIfNeeded();
                RefreshChat();
                // 清除当前选中，避免 XR 的“选中高亮/遮罩”一直留在界面上导致看起来像卡住
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            });
        });
    }

    /// <summary>
    /// 按照 "段1|||段2|||段3" 这样的格式拆分 AI 返回的文本
    /// </summary>
    List<string> ParseStorySegments(string response)
    {
        var segments = new List<string>();
        if (string.IsNullOrEmpty(response)) return segments;

        string[] parts = response.Split(new[] { "|||" }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                segments.Add(trimmed);
            }
        }

        return segments;
    }

    /// <summary>
    /// 当 AI 返回的段数不足时，使用的本地兜底小故事（保证一定有三段）
    /// </summary>
    List<string> GetFallbackStorySegments()
    {
        return new List<string>
        {
            "When you get a small cut, tiny bacteria from the outside see an open door. They slip in and try to make a home inside your body.",
            "Your white blood cells get the alarm. They rush out from your blood vessels like little guards in white armor, looking for the bad guys.",
            "When they find bacteria, white blood cells grab them, swallow them, and break them down. When the bad guys are gone, your cut can heal."
        };
    }

    /// <summary> 对话条数过多时删掉最老的，只保留最近一段，减轻卡顿和可能的遮罩异常 </summary>
    void TrimChatHistoryIfNeeded()
    {
        while (chatHistory.Count > MAX_CHAT_HISTORY_LINES)
            chatHistory.RemoveAt(0);
    }

    void RefreshChat()
    {
        if (chatText == null) return;
        TrimChatHistoryIfNeeded();
        chatText.text = string.Join("\n", chatHistory);
        RectTransform rt = chatText.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, chatText.preferredHeight + 20);
        if (scrollRect) { Canvas.ForceUpdateCanvases(); scrollRect.verticalNormalizedPosition = 0f; }
    }

    /// <summary>
    /// 只保留实际录到的前 recordedSamples 个样本，避免把整段 10 秒（含静音）都发给 Whisper
    /// </summary>
    static AudioClip TrimClipToRecordedLength(AudioClip fullClip, int recordedSamples)
    {
        if (fullClip == null || recordedSamples <= 0) return null;
        int channels = fullClip.channels;
        int totalRecorded = recordedSamples * channels;
        int fullLength = fullClip.samples * channels;
        if (totalRecorded >= fullLength) return fullClip;

        float[] buf = new float[totalRecorded];
        fullClip.GetData(buf, 0);
        AudioClip trimmed = AudioClip.Create("Trimmed", recordedSamples, channels, fullClip.frequency, false);
        trimmed.SetData(buf, 0);
        return trimmed;
    }

    /// <summary>
    /// 将 AudioClip 转为 WAV 字节（16-bit PCM），供 Whisper API 使用
    /// </summary>
    static byte[] AudioClipToWav(AudioClip clip)
    {
        if (clip == null) return null;
        int samples = clip.samples * clip.channels;
        float[] data = new float[samples];
        clip.GetData(data, 0);
        int sampleRate = clip.frequency;
        int numChannels = clip.channels;
        int subChunk2Size = samples * 2;
        int chunkSize = 36 + subChunk2Size;
        using (var ms = new System.IO.MemoryStream())
        {
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(chunkSize);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)numChannels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * numChannels * 2);
                writer.Write((short)(numChannels * 2));
                writer.Write((short)16);
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(subChunk2Size);
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(Mathf.Clamp(data[i], -1f, 1f) * 32767f);
                    writer.Write(s);
                }
            }
            return ms.ToArray();
        }
    }
}
