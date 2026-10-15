using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Builds the study app UI at runtime and keeps entered material on this device.
/// </summary>
public class Manager : MonoBehaviour
{
    [Serializable] private class StudyEntry
    {
        public string id;
        public string subject;
        public string area;
        public string kind;
        public string title;
        public string content;
        public List<string> highlights = new List<string>();
    }

    [Serializable] private class StudyData
    {
        public List<StudyEntry> entries = new List<StudyEntry>();
        public List<WrongAnswer> wrongAnswers = new List<WrongAnswer>();
    }

    [Serializable] private class WrongAnswer
    {
        public string subject;
        public string area;
        public string type;
        public string format;
        public string question;
        public string chosenAnswer;
        public string correctAnswer;
        public string explanation;
        public string sourceTitle;
        public string date;
        public List<string> options = new List<string>();
        public List<string> gradingCriteria = new List<string>();
        public int correctIndex;
    }

    [Serializable] private class AiMaterial
    {
        public string area;
        public string kind;
        public string title;
        public string content;
        public List<string> highlights = new List<string>();
    }

    [Serializable] private class AiQuizRequest
    {
        public string subject;
        public string area;
        public string grade;
        public string difficulty;
        public int questionCount = 10;
        public int multipleChoiceCount = 5;
        public int essayCount = 2;
        public int shortAnswerCount = 3;
        public string instructions;
        public List<AiMaterial> materials = new List<AiMaterial>();
    }

    [Serializable] private class AiQuizResponse
    {
        public List<AiQuestion> questions = new List<AiQuestion>();
    }

    [Serializable] private class AiGradeRequest
    {
        public string grade;
        public string format;
        public string question;
        public string studentAnswer;
        public string expectedAnswer;
        public List<string> gradingCriteria = new List<string>();
    }

    [Serializable] private class AiGradeResponse
    {
        public bool isCorrect;
        public string feedback;
    }

    [Serializable] private class AiErrorResponse
    {
        public string error;
    }

    [Serializable] private class AiQuestion
    {
        public string question;
        public string format;
        public List<string> options = new List<string>();
        public int answerIndex;
        public string answer;
        public List<string> gradingCriteria = new List<string>();
        public string explanation;
        public string type;
        public string area;
        public string sourceTitle;
    }

    [Serializable] private class AiWrongSummaryRequest
    {
        public string grade;
        public List<WrongAnswer> wrongAnswers = new List<WrongAnswer>();
    }

    [Serializable] private class AiWrongSummaryResponse
    {
        public List<AiWrongSummary> summaries = new List<AiWrongSummary>();
    }

    [Serializable] private class AiWrongSummary
    {
        public string subject;
        public string summary;
        public List<string> patterns = new List<string>();
        public List<string> nextSteps = new List<string>();
    }

    private enum Screen { Home, SubjectPicker, AreaPicker, Vocabulary, Memo, Unit, Notes, ReviewList, Incorrect, Learning, QuizSetup, Quiz, DeleteConfirm }
    private enum Flow { Input, Review, Learning }

    private sealed class NavigationState
    {
        public Screen screen;
        public Flow flow;
        public string subject;
        public string area;
        public string unitName;
        public string editingId;
        public string pendingDeleteId;
        public string incorrectFilter;
    }

    private static readonly string[] Subjects = { "국어", "영어", "과학", "역사/사회" };
    private static readonly string[] OptionMarkers = { "①", "②", "③", "④" };
    private static readonly Dictionary<string, string[]> Areas = new Dictionary<string, string[]>
    {
        { "국어", new[] { "어휘", "문법" } },
        { "영어", new[] { "단어", "문법", "표현" } },
        { "과학", new[] { "개념" } },
        { "역사/사회", new[] { "개념" } }
    };

    private readonly Color ink = new Color32(53, 58, 51, 255);
    private readonly Color muted = new Color32(119, 120, 108, 255);
    private readonly Color canvasColor = new Color32(244, 241, 231, 255);
    private readonly Color blue = new Color32(116, 139, 116, 255);
    private readonly Color softSurface = new Color32(239, 236, 224, 255);
    private readonly Color paperColor = new Color32(255, 252, 242, 255);
    private readonly Color lineColor = new Color32(223, 219, 203, 255);
    private readonly Color highlightColor = new Color32(244, 231, 170, 255);
    private readonly Color white = new Color32(252, 251, 246, 255);

    private StudyData data = new StudyData();
    private Font font;
    private Sprite roundedSprite;
    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private RectTransform scrollRectTransform;
    private RectTransform footerRect;
    private Rect lastSafeArea = new Rect(-1, -1, -1, -1);
    private int lastScreenWidth;
    private int lastScreenHeight;
    private int keyboardClosedScreenHeight;
    private RectTransform content;
    private Text statusText;
    private Screen screen = Screen.Home;
    private readonly Stack<NavigationState> screenHistory = new Stack<NavigationState>();
    private NavigationState visibleScreenState;
    private bool navigatingBack;
    private int homeActionIndex;
    private Flow flow = Flow.Input;
    private string subject = "";
    private string area = "";
    private string unitName = "";
    private string notesDraft = "";
    private string editingId = "";
    private string pendingDeleteId = "";
    private readonly List<StudyEntry> draftCards = new List<StudyEntry>();
    private readonly List<string> draftHighlights = new List<string>();
    private InputField wordField;
    private InputField meaningField;
    private InputField memoField;
    private InputField unitField;
    private InputField gradeField;
    private InputField multipleChoiceCountField;
    private InputField essayCountField;
    private InputField shortAnswerCountField;
    private InputField writtenAnswerField;
    private string selectedDifficulty = "중";
    private string multipleChoiceCountText = "5";
    private string essayCountText = "2";
    private string shortAnswerCountText = "3";
    private string writtenAnswer = "";
    private bool writtenAnswerReviewed;
    private bool writtenAnswerCorrect;
    private bool gradingWrittenAnswer;
    private string writtenFeedback = "";
    private readonly List<AiQuestion> quizQuestions = new List<AiQuestion>();
    private List<StudyEntry> quizMaterials = new List<StudyEntry>();
    private readonly List<WrongAnswer> redoWrongAnswers = new List<WrongAnswer>();
    private readonly List<AiWrongSummary> wrongSummaries = new List<AiWrongSummary>();
    private string incorrectFilter = "";
    private string wrongSummaryStatus = "";
    private WrongAnswer selectedWrongForDelete;
    private int quizIndex;
    private int quizCorrectCount;
    private int redoFixedCount;
    private int selectedAnswer = -1;
    private bool generatingQuestions;
    private bool summarizingWrongs;
    private bool redoMode;
    private int memoSelectionStart = -1;
    private int memoSelectionEnd = -1;
    private string memoSelectionText = "";

    [SerializeField] private string aiServerEndpoint = "http://127.0.0.1:8787/quiz";
    [SerializeField] private string aiServerAccessToken = "";

    private string SavePath { get { return Path.Combine(Application.persistentDataPath, "study_content.json"); } }

    private void Start()
    {
        if (Application.isMobilePlatform) UnityEngine.Screen.orientation = ScreenOrientation.Portrait;
        LoadData();
        BuildCanvas();
        ShowHome();
    }

    private void Update()
    {
        UpdateMobileLayout();

        // Keep the selection before tapping the highlighter button moves focus away
        // from the mobile InputField.
        if (memoField == null || !memoField.isFocused) return;
        int anchor = memoField.selectionAnchorPosition;
        int focus = memoField.selectionFocusPosition;
        if (anchor == focus || memoField.text == null) return;
        memoSelectionStart = Mathf.Min(anchor, focus);
        memoSelectionEnd = Mathf.Max(anchor, focus);
        memoSelectionText = memoField.text;
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                StudyData loaded = JsonUtility.FromJson<StudyData>(File.ReadAllText(SavePath));
                if (loaded != null && loaded.entries != null)
                {
                    data = loaded;
                    if (data.wrongAnswers == null) data.wrongAnswers = new List<WrongAnswer>();
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("Could not load study content: " + e.Message); }
    }

    private void SaveData()
    {
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); }
        catch (Exception e) { Debug.LogError("Could not save study content: " + e.Message); SetStatus("저장에 실패했어요. 저장 공간을 확인해 주세요."); }
    }

    private void BuildCanvas()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Roboto", "Arial" }, 30);
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        roundedSprite = CreateRoundedSprite();

        GameObject canvasObject = new GameObject("Study App Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        Image background = canvasObject.AddComponent<Image>();
        background.color = canvasColor;
        background.raycastTarget = false;

        GameObject safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
        safeAreaObject.transform.SetParent(canvas.transform, false);
        safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
        UpdateMobileLayout();

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(events);
        }
    }

    private void UpdateMobileLayout()
    {
        if (safeAreaRoot == null || UnityEngine.Screen.width <= 0 || UnityEngine.Screen.height <= 0) return;

        Rect safeArea = UnityEngine.Screen.safeArea;
        if (safeArea != lastSafeArea || UnityEngine.Screen.width != lastScreenWidth || UnityEngine.Screen.height != lastScreenHeight)
        {
            safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / UnityEngine.Screen.width, safeArea.yMin / UnityEngine.Screen.height);
            safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / UnityEngine.Screen.width, safeArea.yMax / UnityEngine.Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenWidth = UnityEngine.Screen.width;
            lastScreenHeight = UnityEngine.Screen.height;
        }

        if (scrollRectTransform == null) return;

        float footerBottom = 0.025f;
        float footerTop = 0.09f;
        float scrollBottom = footerRect != null ? 0.105f : 0.045f;
        Rect keyboardArea = TouchScreenKeyboard.area;
        bool keyboardVisible = Application.isMobilePlatform && TouchScreenKeyboard.visible && footerRect != null;
        if (!keyboardVisible) keyboardClosedScreenHeight = UnityEngine.Screen.height;
        bool viewportResizedForKeyboard = keyboardVisible && keyboardClosedScreenHeight > UnityEngine.Screen.height * 1.12f;
        bool keyboardAreaAvailable = keyboardArea.height > 1f && safeArea.height > 1f;

        if (keyboardVisible && !viewportResizedForKeyboard && keyboardAreaAvailable)
        {
            footerBottom = Mathf.Clamp((keyboardArea.yMax - safeArea.yMin) / safeArea.height + 0.018f, 0.025f, 0.86f);
            footerTop = Mathf.Min(footerBottom + 0.065f, 0.96f);
            scrollBottom = footerTop + 0.02f;
        }
        else if (keyboardVisible && !viewportResizedForKeyboard)
        {
            // Android reports a zero keyboard area. Reserve the lower part of the
            // screen for the keyboard and keep the form scrollable above it.
            scrollBottom = 0.36f;
        }

        if (footerRect != null)
        {
            footerRect.gameObject.SetActive(!keyboardVisible || viewportResizedForKeyboard || keyboardAreaAvailable);
            footerRect.anchorMin = new Vector2(0.07f, footerBottom);
            footerRect.anchorMax = new Vector2(0.93f, footerTop);
            footerRect.offsetMin = Vector2.zero;
            footerRect.offsetMax = Vector2.zero;
        }

        Vector2 scrollAnchorMin = new Vector2(0.045f, scrollBottom);
        if (scrollRectTransform.anchorMin != scrollAnchorMin)
            scrollRectTransform.anchorMin = scrollAnchorMin;
    }

    private Sprite CreateRoundedSprite()
    {
        const int size = 64;
        const float radius = 14f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Study UI Rounded Sprite";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x - (size - 1) * 0.5f) - (size * 0.5f - radius), 0);
            float dy = Mathf.Max(Mathf.Abs(y - (size - 1) * 0.5f) - (size * 0.5f - radius), 0);
            pixels[y * size + x] = dx * dx + dy * dy <= radius * radius
                ? new Color32(255, 255, 255, 255)
                : new Color32(255, 255, 255, 0);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
    }

    private Image CreateSurface(Transform parent, string objectName, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    private void AddSoftShadow(GameObject target, float alpha = 0.08f)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.20f, 0.19f, 0.15f, alpha);
        shadow.effectDistance = new Vector2(0, -3);
    }

    private void BeginScreen(Screen next, string title, string subtitle = "", string footerLabel = null, UnityEngine.Events.UnityAction footerAction = null)
    {
        if (next == Screen.Home)
            screenHistory.Clear();
        else if (!navigatingBack && next != screen)
            screenHistory.Push(visibleScreenState ?? CaptureNavigationState());

        screen = next;
        scrollRectTransform = null;
        footerRect = null;
        for (int i = safeAreaRoot.childCount - 1; i >= 0; i--) Destroy(safeAreaRoot.GetChild(i).gameObject);
        statusText = null;

        if (next != Screen.Home)
        {
            Image headerSurface = CreateSurface(safeAreaRoot, "Header surface", white);
            RectTransform headerRect = headerSurface.rectTransform;
            headerRect.anchorMin = new Vector2(0.045f, 0.845f);
            headerRect.anchorMax = new Vector2(0.955f, 0.985f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            headerSurface.raycastTarget = false;
            AddSoftShadow(headerSurface.gameObject, 0.045f);
        }

        if (!string.IsNullOrEmpty(title))
        {
            Text heading = CreateText(safeAreaRoot, title, next == Screen.Home ? 50 : 42, ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(next == Screen.Home ? 0.085f : 0.17f, 0.895f);
            headingRect.anchorMax = new Vector2(next == Screen.Home ? 0.915f : 0.82f, 0.965f);
            headingRect.offsetMin = Vector2.zero;
            headingRect.offsetMax = Vector2.zero;
        }

        if (!string.IsNullOrEmpty(subtitle))
        {
            Text sub = CreateText(safeAreaRoot, subtitle, next == Screen.Home ? 25 : 23, muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            RectTransform sr = sub.rectTransform;
            sr.anchorMin = new Vector2(next == Screen.Home ? 0.085f : 0.17f, 0.852f);
            sr.anchorMax = new Vector2(0.915f, 0.895f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;
        }

        if (next != Screen.Home)
        {
            GameObject backObject = new GameObject("Back button", typeof(RectTransform), typeof(Image), typeof(Button));
            backObject.transform.SetParent(safeAreaRoot, false);
            Image backHitArea = backObject.GetComponent<Image>();
            backHitArea.color = new Color(1, 1, 1, 0);
            backHitArea.raycastTarget = true;
            RectTransform backRect = backObject.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.91f, 0.93f);
            backRect.anchorMax = new Vector2(0.91f, 0.93f);
            backRect.sizeDelta = new Vector2(84, 84);
            backRect.anchoredPosition = Vector2.zero;

            Text backLabel = CreateText(backObject.transform, "←", 52, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform backLabelRect = backLabel.rectTransform;
            backLabelRect.anchorMin = Vector2.zero;
            backLabelRect.anchorMax = Vector2.one;
            backLabelRect.offsetMin = Vector2.zero;
            backLabelRect.offsetMax = Vector2.zero;
            backLabel.GetComponent<LayoutElement>().ignoreLayout = true;

            Button backButton = backObject.GetComponent<Button>();
            backButton.targetGraphic = backLabel;
            backButton.onClick.AddListener(NavigateBack);
            ColorBlock backColors = backButton.colors;
            backColors.normalColor = ink;
            backColors.highlightedColor = blue;
            backColors.pressedColor = new Color32(96, 119, 96, 255);
            backColors.selectedColor = blue;
            backColors.colorMultiplier = 1;
            backButton.colors = backColors;
        }

        GameObject scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(FastScrollRect));
        scrollObject.transform.SetParent(safeAreaRoot, false);
        Image scrollBg = scrollObject.GetComponent<Image>();
        scrollBg.color = Color.clear;
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform = scrollRect;
        scrollRect.anchorMin = new Vector2(0.045f, 0.105f);
        scrollRect.anchorMax = new Vector2(0.955f, next == Screen.Home ? 0.985f : string.IsNullOrEmpty(subtitle) ? 0.87f : 0.83f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = next == Screen.Home ? Color.clear : white;
        viewportImage.raycastTarget = true;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 20, 30);
        layout.spacing = 17;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        FastScrollRect scroll = scrollObject.GetComponent<FastScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.dragSpeedMultiplier = 1.8f;
        scroll.scrollSensitivity = 45f;
        scroll.decelerationRate = 0.4f;
        scroll.inertia = true;

        bool vocabularyEntry = next == Screen.Vocabulary && string.IsNullOrEmpty(editingId);
        if (next != Screen.Home && (footerAction != null || vocabularyEntry))
        {
            string buttonLabel = footerLabel ?? (vocabularyEntry ? "입력 완료" : "저장하기");
            UnityEngine.Events.UnityAction buttonAction = footerAction ?? (UnityEngine.Events.UnityAction)FinishDraftCards;
            Button footerButton = CreateButton(safeAreaRoot,
                buttonLabel,
                vocabularyEntry || footerAction != null,
                buttonAction);
            RectTransform footerButtonRect = footerButton.GetComponent<RectTransform>();
            footerRect = footerButtonRect;
            footerButtonRect.anchorMin = new Vector2(0.07f, 0.025f);
            footerButtonRect.anchorMax = new Vector2(0.93f, 0.09f);
            footerButtonRect.offsetMin = Vector2.zero;
            footerButtonRect.offsetMax = Vector2.zero;
        }

        UpdateMobileLayout();
        visibleScreenState = CaptureNavigationState();
    }

    private void NavigateBack()
    {
        if (screenHistory.Count == 0)
        {
            ShowHome();
            return;
        }

        NavigationState previous = screenHistory.Pop();
        RestoreNavigationState(previous);
        navigatingBack = true;
        try
        {
            switch (previous.screen)
            {
                case Screen.Home: ShowHome(); break;
                case Screen.SubjectPicker: ShowSubjectPicker(); break;
                case Screen.AreaPicker: OpenAreaPicker(); break;
                case Screen.Vocabulary: ShowVocabulary(); break;
                case Screen.Memo: ShowMemo(); break;
                case Screen.Unit: ShowUnit(); break;
                case Screen.Notes: ShowNotes(); break;
                case Screen.ReviewList: ShowReviewList(); break;
                case Screen.Incorrect: ShowIncorrectScreen(); break;
                case Screen.Learning: ShowLearningArea(); break;
                case Screen.QuizSetup: ShowQuizSetup(); break;
                case Screen.Quiz: ShowQuizQuestion(); break;
                case Screen.DeleteConfirm: ConfirmDelete(pendingDeleteId); break;
                default: ShowHome(); break;
            }
        }
        finally
        {
            navigatingBack = false;
        }
    }

    private void NavigateBackTo(Screen target)
    {
        while (screenHistory.Count > 0 && screenHistory.Peek().screen != target)
            screenHistory.Pop();

        if (screenHistory.Count > 0) NavigateBack();
        else ShowHome();
    }

    private NavigationState CaptureNavigationState()
    {
        return new NavigationState
        {
            screen = screen,
            flow = flow,
            subject = subject,
            area = area,
            unitName = unitName,
            editingId = editingId,
            pendingDeleteId = pendingDeleteId,
            incorrectFilter = incorrectFilter
        };
    }

    private void RestoreNavigationState(NavigationState state)
    {
        flow = state.flow;
        subject = state.subject;
        area = state.area;
        unitName = state.unitName;
        editingId = state.editingId;
        pendingDeleteId = state.pendingDeleteId;
        incorrectFilter = state.incorrectFilter;
    }

    private Text CreateText(Transform parent, string value, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        LayoutElement layout = go.AddComponent<LayoutElement>();
        int charsPerLine = Mathf.Max(16, Mathf.FloorToInt(700f / (size * 1.15f)));
        int lines = 0;
        foreach (string line in value.Split('\n'))
            lines += Mathf.Max(1, Mathf.CeilToInt(line.Length / (float)charsPerLine));
        layout.minHeight = size * 1.5f;
        layout.preferredHeight = size * 1.55f * Mathf.Max(1, lines);
        return text;
    }

    private Button CreateButton(Transform parent, string label, bool primary, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject("Button - " + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = white;
        AddSoftShadow(go, primary ? 0.085f : 0.04f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = primary ? blue : softSurface;
        colors.highlightedColor = primary ? new Color32(131, 151, 128, 255) : new Color32(231, 228, 216, 255);
        colors.pressedColor = primary ? new Color32(96, 119, 96, 255) : new Color32(222, 218, 205, 255);
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1;
        button.colors = colors;
        button.onClick.AddListener(action);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 96;
        layout.preferredHeight = 108;
        layout.flexibleWidth = 1;
        Text text = CreateText(go.transform, label, 29, primary ? white : ink, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(18, 0);
        tr.offsetMax = new Vector2(-18, 0);
        text.GetComponent<LayoutElement>().ignoreLayout = true;
        return button;
    }

    private InputField CreateInput(string label, string placeholder, bool multiline, string initial = "", bool ruledPaper = false)
    {
        CreateText(content, label, 25, ink, FontStyle.Bold);
        GameObject box = new GameObject("Input - " + label, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        box.transform.SetParent(content, false);
        Image background = box.GetComponent<Image>();
        background.sprite = roundedSprite;
        background.type = Image.Type.Sliced;
        background.color = ruledPaper ? paperColor : white;
        AddSoftShadow(box, 0.045f);
        LayoutElement layout = box.GetComponent<LayoutElement>();
        layout.minHeight = multiline ? 360 : 92;
        layout.preferredHeight = multiline ? 430 : 102;
        layout.flexibleWidth = 1;
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta = new Vector2(0, layout.preferredHeight);

        if (ruledPaper)
        {
            GameObject margin = new GameObject("Paper margin", typeof(RectTransform), typeof(Image));
            margin.transform.SetParent(box.transform, false);
            Image marginImage = margin.GetComponent<Image>();
            marginImage.color = new Color32(199, 157, 143, 155);
            marginImage.raycastTarget = false;
            RectTransform marginRect = margin.GetComponent<RectTransform>();
            marginRect.anchorMin = new Vector2(0.13f, 0.055f);
            marginRect.anchorMax = new Vector2(0.13f, 0.945f);
            marginRect.sizeDelta = new Vector2(2, 0);

            for (float y = 62; y < layout.preferredHeight - 12; y += 48)
            {
                GameObject rule = new GameObject("Paper rule", typeof(RectTransform), typeof(Image));
                rule.transform.SetParent(box.transform, false);
                Image ruleImage = rule.GetComponent<Image>();
                ruleImage.color = lineColor;
                ruleImage.raycastTarget = false;
                RectTransform ruleRect = rule.GetComponent<RectTransform>();
                ruleRect.anchorMin = new Vector2(0.14f, 1);
                ruleRect.anchorMax = new Vector2(0.965f, 1);
                ruleRect.pivot = new Vector2(0.5f, 0.5f);
                ruleRect.sizeDelta = new Vector2(0, 2);
                ruleRect.anchoredPosition = new Vector2(0, -y);
            }
        }

        Text valueText = CreateText(box.transform, initial, 27, ink, FontStyle.Normal, TextAnchor.UpperLeft);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(ruledPaper ? 116 : 20, 16);
        valueRect.offsetMax = new Vector2(-20, -16);
        valueText.GetComponent<LayoutElement>().ignoreLayout = true;
        valueText.raycastTarget = true;

        Text placeholderText = CreateText(box.transform, placeholder, 25, muted, FontStyle.Normal, TextAnchor.UpperLeft);
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(ruledPaper ? 116 : 20, 16);
        placeholderRect.offsetMax = new Vector2(-20, -16);
        placeholderText.GetComponent<LayoutElement>().ignoreLayout = true;

        InputField input = box.GetComponent<InputField>();
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.text = initial;
        input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        input.targetGraphic = box.GetComponent<Image>();
        input.caretColor = blue;
        input.selectionColor = new Color32(143, 164, 133, 105);
        return input;
    }

    private InputField CreateAnchoredInput(Transform parent, string placeholder, string initial, Vector2 anchorMin, Vector2 anchorMax, int fontSize, bool multiline = false)
    {
        GameObject box = new GameObject("Card input", typeof(RectTransform), typeof(Image), typeof(InputField));
        box.transform.SetParent(parent, false);
        Image background = box.GetComponent<Image>();
        background.color = new Color(1, 1, 1, 0);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = anchorMin;
        boxRect.anchorMax = anchorMax;
        boxRect.offsetMin = Vector2.zero;
        boxRect.offsetMax = Vector2.zero;

        TextAnchor textAnchor = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        Text valueText = CreateText(box.transform, initial, fontSize, ink, FontStyle.Normal, textAnchor);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(4, 4);
        valueRect.offsetMax = new Vector2(-4, -4);
        valueText.GetComponent<LayoutElement>().ignoreLayout = true;
        valueText.raycastTarget = true;

        Text placeholderText = CreateText(box.transform, placeholder, fontSize - 2, muted, FontStyle.Normal, textAnchor);
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(4, 4);
        placeholderRect.offsetMax = new Vector2(-4, -4);
        placeholderText.GetComponent<LayoutElement>().ignoreLayout = true;

        InputField input = box.GetComponent<InputField>();
        input.textComponent = valueText;
        input.placeholder = placeholderText;
        input.text = initial;
        input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        input.targetGraphic = background;
        input.caretColor = blue;
        input.selectionColor = new Color32(143, 164, 133, 105);
        return input;
    }

    private void CreateVocabularyCardInputs(StudyEntry current)
    {
        CreateText(content, "새 플래시카드", 23, muted, FontStyle.Bold);
        GameObject card = new GameObject("Vocabulary flashcard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(content, false);
        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = roundedSprite;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = paperColor;
        AddSoftShadow(card, 0.065f);
        LayoutElement cardLayout = card.GetComponent<LayoutElement>();
        cardLayout.minHeight = 380;
        cardLayout.preferredHeight = 450;
        cardLayout.flexibleWidth = 1;
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 450);

        GameObject dividerObject = new GameObject("Card center divider", typeof(RectTransform), typeof(Image));
        dividerObject.transform.SetParent(card.transform, false);
        Image divider = dividerObject.GetComponent<Image>();
        divider.color = lineColor;
        divider.raycastTarget = false;
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0.5f, 0.08f);
        dividerRect.anchorMax = new Vector2(0.5f, 0.92f);
        dividerRect.sizeDelta = new Vector2(2, 0);
        dividerRect.anchoredPosition = Vector2.zero;
        divider.raycastTarget = false;

        CreateCardLabel(card.transform, subject == "국어" ? "어휘  /  단어" : "단어  /  WORD", new Vector2(0.08f, 0.82f), new Vector2(0.46f, 0.94f));
        CreateCardLabel(card.transform, "뜻  /  MEANING", new Vector2(0.54f, 0.82f), new Vector2(0.92f, 0.94f));
        wordField = CreateAnchoredInput(card.transform, "단어를 입력하세요", current != null ? current.title : "", new Vector2(0.08f, 0.1f), new Vector2(0.46f, 0.8f), 29);
        meaningField = CreateAnchoredInput(card.transform, "뜻을 입력하세요", current != null ? current.content : "", new Vector2(0.54f, 0.1f), new Vector2(0.92f, 0.8f), 25, true);
    }

    private void CreateCardLabel(Transform parent, string value, Vector2 min, Vector2 max)
    {
        Text label = CreateText(parent, value, 19, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        label.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    private void CreateHomeAction(string title, string detail, Color accent, bool primary, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject("Home action - " + title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        bool centeredHomeAction = screen == Screen.Home;
        go.transform.SetParent(centeredHomeAction ? safeAreaRoot : content, false);
        Image image = go.GetComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = white;
        AddSoftShadow(go, centeredHomeAction ? (primary ? 0.12f : 0.07f) : (primary ? 0.085f : 0.04f));
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = primary ? blue : softSurface;
        colors.highlightedColor = primary ? new Color32(131, 151, 128, 255) : new Color32(231, 228, 216, 255);
        colors.pressedColor = primary ? new Color32(96, 119, 96, 255) : new Color32(222, 218, 205, 255);
        button.colors = colors;
        button.onClick.AddListener(action);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 138;
        layout.preferredHeight = 150;
        if (centeredHomeAction)
        {
            layout.ignoreLayout = true;
            RectTransform actionRect = go.GetComponent<RectTransform>();
            int column = homeActionIndex % 2;
            int row = homeActionIndex / 2;
            float left = column == 0 ? 0.06f : 0.515f;
            float right = column == 0 ? 0.485f : 0.94f;
            float top = row == 0 ? 0.675f : 0.445f;
            actionRect.anchorMin = new Vector2(left, top - 0.20f);
            actionRect.anchorMax = new Vector2(right, top);
            actionRect.offsetMin = Vector2.zero;
            actionRect.offsetMax = Vector2.zero;
            homeActionIndex++;
        }

        Text titleText = CreateText(go.transform, title, centeredHomeAction ? 31 : 29, primary ? white : ink, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = centeredHomeAction ? new Vector2(0.09f, 0.68f) : new Vector2(0.26f, 0.47f);
        titleRect.anchorMax = centeredHomeAction ? new Vector2(0.76f, 0.94f) : new Vector2(0.82f, 0.8f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        titleText.GetComponent<LayoutElement>().ignoreLayout = true;

        Text detailText = CreateText(go.transform, detail, centeredHomeAction ? 21 : 20, primary ? new Color32(233, 238, 227, 255) : muted, FontStyle.Normal, TextAnchor.UpperLeft);
        RectTransform detailRect = detailText.rectTransform;
        detailRect.anchorMin = centeredHomeAction ? new Vector2(0.09f, 0.28f) : new Vector2(0.26f, 0.19f);
        detailRect.anchorMax = centeredHomeAction ? new Vector2(0.91f, 0.64f) : new Vector2(0.82f, 0.49f);
        detailRect.offsetMin = Vector2.zero;
        detailRect.offsetMax = Vector2.zero;
        detailText.GetComponent<LayoutElement>().ignoreLayout = true;

        Text arrow = CreateText(go.transform, "›", 39, primary ? white : accent, FontStyle.Normal, TextAnchor.MiddleCenter);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = centeredHomeAction ? new Vector2(0.78f, 0.70f) : new Vector2(0.86f, 0.25f);
        arrowRect.anchorMax = centeredHomeAction ? new Vector2(0.92f, 0.94f) : new Vector2(0.95f, 0.75f);
        arrowRect.offsetMin = Vector2.zero;
        arrowRect.offsetMax = Vector2.zero;
        arrow.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    private void AddStatus(string initial = "")
    {
        statusText = CreateText(content, initial, 22, muted, FontStyle.Normal);
    }

    private void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
        else Debug.Log(value);
    }

    private void ShowHome()
    {
        subject = "";
        area = "";
        editingId = "";
        homeActionIndex = 0;
        BeginScreen(Screen.Home, "오늘은 무엇을 공부할까요?", "자료를 정리하고, 문제를 풀며 꾸준히 복습해요.");
        Image sectionMarker = CreateSurface(safeAreaRoot, "Home section marker", blue);
        RectTransform markerRect = sectionMarker.rectTransform;
        markerRect.anchorMin = new Vector2(0.065f, 0.735f);
        markerRect.anchorMax = new Vector2(0.075f, 0.79f);
        markerRect.offsetMin = Vector2.zero;
        markerRect.offsetMax = Vector2.zero;
        sectionMarker.raycastTarget = false;
        Text sectionTitle = CreateText(safeAreaRoot, "바로 시작하기", 32, ink, FontStyle.Bold);
        RectTransform sectionRect = sectionTitle.rectTransform;
        sectionRect.anchorMin = new Vector2(0.095f, 0.72f);
        sectionRect.anchorMax = new Vector2(0.93f, 0.80f);
        sectionRect.offsetMin = Vector2.zero;
        sectionRect.offsetMax = Vector2.zero;
        sectionTitle.GetComponent<LayoutElement>().ignoreLayout = true;
        CreateHomeAction("학습하기", "저장한 자료로 문제를 풀어보세요", new Color32(132, 151, 127, 255), true, () => OpenSubjectPicker(Flow.Learning));
        CreateHomeAction("입력하기", "단어, 문법, 개념을 정리해요", new Color32(157, 161, 126, 255), false, () => OpenSubjectPicker(Flow.Input));
        CreateHomeAction("입력 내용 확인", "저장한 학습 자료를 관리해요", new Color32(164, 148, 125, 255), false, () => OpenSubjectPicker(Flow.Review));
        CreateHomeAction("틀린 문제 확인하기", "과목별 오답과 AI 분석을 다시 봐요", new Color32(179, 137, 111, 255), false, ShowIncorrect);
    }

    private void OpenSubjectPicker(Flow nextFlow)
    {
        flow = nextFlow;
        ShowSubjectPicker();
    }

    private void ShowSubjectPicker()
    {
        string title = flow == Flow.Input ? "과목 선택" : flow == Flow.Review ? "확인할 과목 선택" : "학습할 과목 선택";
        BeginScreen(Screen.SubjectPicker, title, "과목을 선택해 주세요.");
        foreach (string item in Subjects)
        {
            string selected = item;
            CreateButton(content, selected, false, () => OnSubjectSelected(selected));
        }
    }

    private void OnSubjectSelected(string selected)
    {
        subject = selected;
        if (Areas.TryGetValue(subject, out string[] options) && options.Length > 1)
        {
            OpenAreaPicker();
            return;
        }
        area = options != null && options.Length == 1 && flow != Flow.Learning ? options[0] : "";
        ContinueAfterArea();
    }

    private void OpenAreaPicker()
    {
        string subtitle = flow == Flow.Learning ? "문제를 풀 범위를 선택해 주세요." : "확인하거나 입력할 세부 영역을 선택해 주세요.";
        BeginScreen(Screen.AreaPicker, subject + " 영역", subtitle);
        if (flow == Flow.Learning)
        {
            CreateButton(content, "전체 영역", true, () => { area = ""; ContinueAfterArea(); });
        }
        foreach (string item in Areas[subject])
        {
            string selected = item;
            CreateButton(content, selected, false, () => { area = selected; ContinueAfterArea(); });
        }
    }

    private void ContinueAfterArea()
    {
        if (flow == Flow.Input)
        {
            editingId = "";
            if ((subject == "국어" && area == "어휘") || (subject == "영어" && area == "단어"))
            {
                draftCards.Clear();
                ShowVocabulary();
            }
            else if (subject == "과학" || subject == "역사/사회")
            {
                unitName = "";
                notesDraft = "";
                draftHighlights.Clear();
                ShowUnit();
            }
            else ShowMemo();
        }
        else if (flow == Flow.Review) ShowReviewList();
        else ShowLearningArea();
    }

    private void ShowVocabulary()
    {
        bool editing = !string.IsNullOrEmpty(editingId);
        StudyEntry current = editing ? FindEntry(editingId) : null;
        string noun = subject == "국어" ? "어휘" : "단어";
        BeginScreen(Screen.Vocabulary, editing ? noun + " 수정" : noun + " 입력", editing ? "단어와 뜻을 고친 뒤 저장해 주세요." : "단어와 뜻을 한 쌍씩 카드처럼 입력해 주세요.");
        CreateVocabularyCardInputs(current);
        if (!editing)
        {
            CreateButton(content, "다음 단어 입력", true, AddDraftCard);
            CreateText(content, "입력한 카드 " + draftCards.Count + "개", 24, ink, FontStyle.Bold);
            foreach (StudyEntry card in draftCards)
                CreateText(content, "• " + card.title + "  —  " + card.content, 22, muted);
            AddStatus();
        }
        else
        {
            AddStatus();
            CreateButton(content, "수정 저장", true, SaveEditedCard);
        }
    }

    private void AddDraftCard()
    {
        string word = wordField != null ? wordField.text.Trim() : "";
        string meaning = meaningField != null ? meaningField.text.Trim() : "";
        if (word.Length == 0 || meaning.Length == 0) { SetStatus("단어와 뜻을 모두 입력해 주세요."); return; }
        draftCards.Add(new StudyEntry { id = Guid.NewGuid().ToString("N"), subject = subject, area = area, kind = "vocabulary", title = word, content = meaning });
        ShowVocabulary();
    }

    private void FinishDraftCards()
    {
        string word = wordField != null ? wordField.text.Trim() : "";
        string meaning = meaningField != null ? meaningField.text.Trim() : "";
        if (draftCards.Count == 0 && word.Length == 0 && meaning.Length == 0)
        {
            ShowHome();
            return;
        }

        if (word.Length > 0 || meaning.Length > 0)
        {
            if (word.Length == 0 || meaning.Length == 0) { SetStatus("마지막 카드도 단어와 뜻을 모두 입력해 주세요."); return; }
            draftCards.Add(new StudyEntry { id = Guid.NewGuid().ToString("N"), subject = subject, area = area, kind = "vocabulary", title = word, content = meaning });
        }
        if (draftCards.Count == 0) { SetStatus("저장할 단어를 입력해 주세요."); return; }
        data.entries.AddRange(draftCards);
        SaveData();
        ShowHome();
    }

    private void SaveEditedCard()
    {
        StudyEntry current = FindEntry(editingId);
        if (current == null) { ShowHome(); return; }
        if (wordField.text.Trim().Length == 0 || meaningField.text.Trim().Length == 0) { SetStatus("단어와 뜻을 모두 입력해 주세요."); return; }
        current.title = wordField.text.Trim();
        current.content = meaningField.text.Trim();
        SaveData();
        NavigateBack();
    }

    private void ShowMemo()
    {
        StudyEntry current = string.IsNullOrEmpty(editingId) ? null : FindEntry(editingId);
        string key = subject + "|" + area + "|memo";
        if (current == null) current = data.entries.FirstOrDefault(e => EntryKey(e) == key);
        BeginScreen(Screen.Memo, subject + " · " + area, "메모장에 내용을 직접 입력해 주세요.", "저장하기", () => SaveMemo(current));
        memoField = CreateInput("학습 메모", "여기에 학습 내용을 입력하세요.", true, current != null ? current.content : "", true);
        AddStatus();
    }

    private void SaveMemo(StudyEntry current)
    {
        bool returnToReview = !string.IsNullOrEmpty(editingId);
        string text = memoField != null ? memoField.text.Trim() : "";
        if (text.Length == 0) { SetStatus("저장할 내용을 입력해 주세요."); return; }
        if (current == null) current = data.entries.FirstOrDefault(e => e.subject == subject && e.area == area && e.kind == "memo");
        if (current == null)
        {
            current = new StudyEntry { id = Guid.NewGuid().ToString("N"), subject = subject, area = area, kind = "memo", title = subject + " " + area };
            data.entries.Add(current);
        }
        current.content = text;
        SaveData();
        if (returnToReview) NavigateBackTo(Screen.ReviewList); else ShowHome();
    }

    private void ShowUnit()
    {
        StudyEntry current = string.IsNullOrEmpty(editingId) ? null : FindEntry(editingId);
        BeginScreen(Screen.Unit, subject + " 단원", "단원명을 먼저 입력해 주세요.");
        unitField = CreateInput("단원명", "예: 물질의 상태 변화", false, current != null ? current.title : unitName);
        AddStatus();
        CreateButton(content, "다음 · 학습 내용 입력", true, () =>
        {
            unitName = unitField.text.Trim();
            if (unitName.Length == 0) { SetStatus("단원명을 입력해 주세요."); return; }
            visibleScreenState = CaptureNavigationState();
            ShowNotes();
        });
    }

    private void ShowNotes()
    {
        StudyEntry current = string.IsNullOrEmpty(editingId) ? null : FindEntry(editingId);
        BeginScreen(Screen.Notes, subject + " · " + unitName, "메모와 형광펜 표시를 AI 문제 출제 참고 자료로 저장합니다.", "저장하기", () => SaveUnitNotes(current));
        string initialNotes = notesDraft.Length > 0 ? notesDraft : current != null ? current.content : "";
        memoField = CreateInput("학습 메모", "단원에서 배운 내용을 모두 적어 주세요.", true, initialNotes, true);
        memoField.onValueChanged.AddListener(_ =>
        {
            memoSelectionStart = -1;
            memoSelectionEnd = -1;
            memoSelectionText = "";
        });
        memoSelectionStart = -1;
        memoSelectionEnd = -1;
        memoSelectionText = "";
        CreateButton(content, "선택한 부분 형광펜 칠하기", false, HighlightSelection);
        CreateText(content, "노란색으로 표시된 문장 " + draftHighlights.Count + "개", 23, ink, FontStyle.Bold);
        foreach (string marked in draftHighlights) CreateHighlightPreview(marked);
        AddStatus("메모에서 중요한 문장을 길게 눌러 선택한 뒤 버튼을 누르세요. 선택한 문장은 아래에 노란색으로 보여요.");
    }

    private void HighlightSelection()
    {
        if (memoField == null) return;
        int start = memoSelectionStart;
        int end = memoSelectionEnd;
        if (memoSelectionText != memoField.text)
        {
            int anchor = memoField.selectionAnchorPosition;
            int focus = memoField.selectionFocusPosition;
            start = Mathf.Min(anchor, focus);
            end = Mathf.Max(anchor, focus);
        }
        if (start < 0 || end <= start || start >= memoField.text.Length) { SetStatus("먼저 메모에서 중요한 문장을 선택해 주세요."); return; }
        end = Mathf.Min(end, memoField.text.Length);
        string selected = memoField.text.Substring(start, end - start).Trim();
        if (selected.Length == 0) { SetStatus("선택한 문장을 찾지 못했어요."); return; }
        notesDraft = memoField.text;
        if (!draftHighlights.Contains(selected)) draftHighlights.Add(selected);
        ShowNotes();
    }

    private void CreateHighlightPreview(string value)
    {
        GameObject chip = new GameObject("Highlighted text", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chip.transform.SetParent(content, false);
        Image chipImage = chip.GetComponent<Image>();
        chipImage.sprite = roundedSprite;
        chipImage.type = Image.Type.Sliced;
        chipImage.color = highlightColor;
        AddSoftShadow(chip, 0.035f);
        LayoutElement layout = chip.GetComponent<LayoutElement>();
        layout.minHeight = 62;
        layout.preferredHeight = Mathf.Max(72, Mathf.Ceil(value.Length / 30f) * 36f + 16f);
        layout.flexibleWidth = 1;
        Text text = CreateText(chip.transform, value, 21, ink, FontStyle.Normal);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18, 4);
        rect.offsetMax = new Vector2(-18, -4);
        text.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    private void SaveUnitNotes(StudyEntry current)
    {
        bool returnToReview = !string.IsNullOrEmpty(editingId);
        string text = memoField != null ? memoField.text.Trim() : "";
        if (unitName.Trim().Length == 0) { SetStatus("단원명을 입력해 주세요."); return; }
        if (text.Length == 0) { SetStatus("학습 내용을 입력해 주세요."); return; }
        if (current == null) current = data.entries.FirstOrDefault(e => e.subject == subject && e.area == area && e.kind == "unit" && e.title == unitName.Trim());
        if (current == null)
        {
            current = new StudyEntry { id = Guid.NewGuid().ToString("N"), subject = subject, area = area, kind = "unit", title = unitName.Trim() };
            data.entries.Add(current);
        }
        current.title = unitName.Trim();
        current.content = text;
        current.highlights = new List<string>(draftHighlights);
        notesDraft = "";
        SaveData();
        if (returnToReview) NavigateBackTo(Screen.ReviewList); else ShowHome();
    }

    private void ShowReviewList()
    {
        BeginScreen(Screen.ReviewList, subject + " · " + area, "입력한 내용을 확인하고 수정할 수 있어요.");
        List<StudyEntry> rows = data.entries.Where(e => e.subject == subject && e.area == area).ToList();
        if (rows.Count == 0)
        {
            CreateText(content, "아직 입력한 내용이 없어요.", 26, muted);
            return;
        }
        foreach (StudyEntry row in rows)
        {
            StudyEntry selected = row;
            CreateText(content, EntryHeading(row), 27, ink, FontStyle.Bold);
            CreateText(content, Preview(row), 21, muted);
            CreateButton(content, "수정", false, () => EditEntry(selected));
            CreateButton(content, "삭제", false, () => ConfirmDelete(selected.id));
        }
    }

    private string EntryHeading(StudyEntry entry)
    {
        if (entry.kind == "vocabulary") return entry.title + " · " + entry.area;
        if (entry.kind == "unit") return "단원: " + entry.title;
        return entry.title;
    }

    private string Preview(StudyEntry entry)
    {
        string result = entry.kind == "vocabulary" ? "뜻: " + entry.content : entry.content;
        if (entry.kind == "unit" && entry.highlights != null && entry.highlights.Count > 0)
            result += "\n형광펜 표시: " + string.Join(" / ", entry.highlights);
        if (result.Length > 240) result = result.Substring(0, 240) + "…";
        return result;
    }

    private void EditEntry(StudyEntry entry)
    {
        editingId = entry.id;
        subject = entry.subject;
        area = entry.area;
        notesDraft = "";
        draftHighlights.Clear();
        if (entry.kind == "unit" && entry.highlights != null) draftHighlights.AddRange(entry.highlights);
        if (entry.kind == "vocabulary") ShowVocabulary();
        else if (entry.kind == "unit") { unitName = entry.title; ShowUnit(); }
        else ShowMemo();
    }

    private void ConfirmDelete(string id)
    {
        pendingDeleteId = id;
        StudyEntry entry = FindEntry(id);
        BeginScreen(Screen.DeleteConfirm, "내용을 삭제할까요?", entry != null ? EntryHeading(entry) : "");
        CreateText(content, "삭제하면 이 항목을 다시 복구할 수 없어요.", 24, muted);
        CreateButton(content, "삭제", true, () =>
        {
            data.entries.RemoveAll(e => e.id == pendingDeleteId);
            SaveData();
            NavigateBack();
        });
        CreateButton(content, "취소", false, NavigateBack);
    }

    private StudyEntry FindEntry(string id) { return data.entries.FirstOrDefault(e => e.id == id); }
    private string EntryKey(StudyEntry e) { return e.subject + "|" + e.area + "|" + e.kind; }

    private void ShowIncorrect()
    {
        incorrectFilter = "";
        selectedWrongForDelete = null;
        ShowIncorrectScreen();
    }

    private void FilterIncorrect(string selectedSubject)
    {
        incorrectFilter = selectedSubject;
        selectedWrongForDelete = null;
        ShowIncorrectScreen();
    }

    private void ShowIncorrectScreen()
    {
        BeginScreen(Screen.Incorrect, "틀린 문제 확인하기", "문제를 길게 누르면 삭제할 수 있어요. 과목별로 다시 풀어보세요.");
        List<WrongAnswer> allWrong = data.wrongAnswers ?? new List<WrongAnswer>();
        List<WrongAnswer> visibleWrong = allWrong
            .Where(item => string.IsNullOrEmpty(incorrectFilter) || item.subject == incorrectFilter)
            .OrderByDescending(item => item.date)
            .ToList();

        CreateText(content, "과목별 오답", 24, ink, FontStyle.Bold);
        CreateButton(content, "전체 과목 · " + allWrong.Count, string.IsNullOrEmpty(incorrectFilter), () => FilterIncorrect(""));
        foreach (string item in Subjects)
        {
            string selected = item;
            int count = allWrong.Count(wrong => wrong.subject == selected);
            CreateButton(content, selected + " · " + count, incorrectFilter == selected, () => FilterIncorrect(selected));
        }

        CreateText(content, string.IsNullOrEmpty(incorrectFilter) ? "모든 오답 " + visibleWrong.Count + "개" : incorrectFilter + " 오답 " + visibleWrong.Count + "개",
            24, ink, FontStyle.Bold);
        if (visibleWrong.Count == 0)
        {
            CreateText(content, "이 범위에 모인 오답이 아직 없어요.", 22, muted);
        }
        else
        {
            for (int i = 0; i < visibleWrong.Count; i++)
            {
                CreateWrongAnswerCard(visibleWrong[i], i + 1);
            }
            CreateButton(content, "오답 고치기 · " + visibleWrong.Count + "문제", true, () => StartWrongCorrection(visibleWrong));
        }

        CreateText(content, "과목별 AI 오답 분석", 25, ink, FontStyle.Bold);
        foreach (AiWrongSummary summary in wrongSummaries.Where(item => string.IsNullOrEmpty(incorrectFilter) || item.subject == incorrectFilter))
        {
            CreateText(content, summary.subject + "\n" + summary.summary, 22, ink, FontStyle.Bold);
            foreach (string pattern in summary.patterns) CreateText(content, "• 자주 틀린 내용: " + pattern, 20, muted);
            foreach (string nextStep in summary.nextSteps) CreateText(content, "• 복습 방법: " + nextStep, 20, muted);
        }

        CreateButton(content, summarizingWrongs ? "과목별 분석 중…" : "AI로 과목별 오답 요약하기", false, SummarizeWrongAnswers);
        AddStatus(wrongSummaryStatus);
    }

    private void CreateWrongAnswerCard(WrongAnswer wrong, int problemNumber)
    {
        GameObject card = new GameObject("Wrong answer card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(content, false);
        Image background = card.GetComponent<Image>();
        background.sprite = roundedSprite;
        background.type = Image.Type.Sliced;
        background.color = white;
        AddSoftShadow(card, 0.045f);
        LayoutElement cardSize = card.GetComponent<LayoutElement>();
        cardSize.minHeight = 160;
        cardSize.flexibleWidth = 1;
        VerticalLayoutGroup cardLayout = card.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(18, 18, 16, 18);
        cardLayout.spacing = 8;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        LongPressGesture longPress = card.AddComponent<LongPressGesture>();
        longPress.onLongPress = () =>
        {
            selectedWrongForDelete = wrong;
            ShowIncorrectScreen();
        };

        GameObject header = new GameObject("Wrong answer heading", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(card.transform, false);
        LayoutElement headerSize = header.GetComponent<LayoutElement>();
        headerSize.minHeight = 54;
        headerSize.preferredHeight = 54;
        HorizontalLayoutGroup headerLayout = header.GetComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = false;

        GameObject numberBox = new GameObject("Crossed problem number", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        numberBox.transform.SetParent(header.transform, false);
        Image numberBackground = numberBox.GetComponent<Image>();
        numberBackground.sprite = roundedSprite;
        numberBackground.type = Image.Type.Sliced;
        numberBackground.color = white;
        LayoutElement numberSize = numberBox.GetComponent<LayoutElement>();
        numberSize.minWidth = 68;
        numberSize.preferredWidth = 68;
        numberSize.minHeight = 50;
        numberSize.preferredHeight = 50;
        Text numberText = CreateText(numberBox.transform, problemNumber.ToString("00"), 25, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
        numberText.rectTransform.anchorMin = Vector2.zero;
        numberText.rectTransform.anchorMax = Vector2.one;
        numberText.rectTransform.offsetMin = Vector2.zero;
        numberText.rectTransform.offsetMax = Vector2.zero;
        numberText.GetComponent<LayoutElement>().ignoreLayout = true;
        AddCrayonHatch(numberBox.transform);

        string heading = wrong.subject + " · " + wrong.area + " · " + wrong.type;
        Text headingText = CreateText(header.transform, heading, 20, ink, FontStyle.Bold);
        LayoutElement headingSize = headingText.GetComponent<LayoutElement>();
        headingSize.minHeight = 50;
        headingSize.preferredHeight = 50;
        headingSize.flexibleWidth = 1;

        CreateText(card.transform, wrong.question, 23, ink, FontStyle.Bold);
        CreateText(card.transform, "내 답  " + wrong.chosenAnswer, 20, muted);
        CreateText(card.transform, "정답  " + wrong.correctAnswer, 20, blue, FontStyle.Bold);
        if (!string.IsNullOrEmpty(wrong.sourceTitle)) CreateText(card.transform, "자료  " + wrong.sourceTitle, 18, muted);
        if (!string.IsNullOrEmpty(wrong.explanation)) CreateText(card.transform, wrong.explanation, 20, muted);

        if (selectedWrongForDelete == wrong)
        {
            CreateText(card.transform, "이 오답을 목록에서 삭제할까요?", 19, muted, FontStyle.Bold);
            GameObject actions = new GameObject("Wrong answer delete actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            actions.transform.SetParent(card.transform, false);
            LayoutElement actionsSize = actions.GetComponent<LayoutElement>();
            actionsSize.minHeight = 96;
            actionsSize.preferredHeight = 96;
            HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 12;
            actionsLayout.childControlWidth = true;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = true;
            actionsLayout.childForceExpandHeight = false;
            CreateButton(actions.transform, "삭제하기", true, () => DeleteWrongAnswer(wrong));
            CreateButton(actions.transform, "취소", false, () =>
            {
                selectedWrongForDelete = null;
                ShowIncorrectScreen();
            });
        }
    }

    private void DeleteWrongAnswer(WrongAnswer wrong)
    {
        if (data.wrongAnswers != null && data.wrongAnswers.Remove(wrong))
        {
            wrongSummaries.Clear();
            wrongSummaryStatus = "";
            SaveData();
        }
        selectedWrongForDelete = null;
        ShowIncorrectScreen();
    }

    private void AddCrayonHatch(Transform parent)
    {
        GameObject stroke = new GameObject("Red crayon stroke", typeof(RectTransform), typeof(Image));
        stroke.transform.SetParent(parent, false);
        Image image = stroke.GetComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Simple;
        image.color = new Color32(190, 48, 54, 205);
        image.raycastTarget = false;
        RectTransform rect = stroke.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(48, 4);
        rect.anchoredPosition = new Vector2(1, 0);
        rect.localRotation = Quaternion.Euler(0, 0, 27);
    }

    private void StartWrongCorrection(List<WrongAnswer> selectedWrong)
    {
        redoWrongAnswers.Clear();
        redoWrongAnswers.AddRange(selectedWrong);
        quizQuestions.Clear();
        foreach (WrongAnswer wrong in redoWrongAnswers)
        {
            bool multipleChoice = string.IsNullOrEmpty(wrong.format) || wrong.format == "multiple_choice";
            List<string> options = !multipleChoice
                ? new List<string>()
                : wrong.options != null && wrong.options.Count >= 2
                    ? new List<string>(wrong.options)
                    : new List<string> { wrong.correctAnswer, wrong.chosenAnswer };
            int correctIndex = multipleChoice && wrong.options != null && wrong.options.Count >= 2 && wrong.correctIndex >= 0 && wrong.correctIndex < options.Count
                ? wrong.correctIndex
                : multipleChoice ? 0 : -1;
            quizQuestions.Add(new AiQuestion
            {
                question = wrong.question,
                format = wrong.format,
                options = options,
                answerIndex = correctIndex,
                answer = wrong.correctAnswer,
                gradingCriteria = wrong.gradingCriteria,
                explanation = wrong.explanation,
                type = wrong.type,
                area = wrong.area,
                sourceTitle = wrong.sourceTitle
            });
        }
        if (quizQuestions.Count == 0) return;
        redoMode = true;
        quizIndex = 0;
        quizCorrectCount = 0;
        redoFixedCount = 0;
        selectedAnswer = -1;
        writtenAnswer = "";
        writtenAnswerReviewed = false;
        writtenAnswerCorrect = false;
        writtenFeedback = "";
        gradingWrittenAnswer = false;
        ShowQuizQuestion();
    }

    private void SummarizeWrongAnswers()
    {
        if (summarizingWrongs) return;
        List<WrongAnswer> wrong = (data.wrongAnswers ?? new List<WrongAnswer>()).ToList();
        if (wrong.Count == 0)
        {
            wrongSummaryStatus = "분석할 오답을 먼저 모아 주세요.";
            ShowIncorrectScreen();
            return;
        }

        string endpoint = (aiServerEndpoint ?? "").Trim();
        Uri endpointUri;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            wrongSummaryStatus = "AI 서버 설정을 확인해 주세요.";
            ShowIncorrectScreen();
            return;
        }

        string endpointBase = endpoint.EndsWith("/quiz", StringComparison.OrdinalIgnoreCase)
            ? endpoint.Substring(0, endpoint.Length - "/quiz".Length).TrimEnd('/')
            : endpoint.TrimEnd('/');
        string summaryEndpoint = endpointBase + "/wrong-summary";
        string token = (aiServerAccessToken ?? "").Trim();
        AiWrongSummaryRequest payload = new AiWrongSummaryRequest
        {
            grade = PlayerPrefs.GetString("study_grade", "학년 정보 없음"),
            wrongAnswers = wrong
        };
        summarizingWrongs = true;
        wrongSummaryStatus = "과목별 오답 유형을 AI가 정리하고 있어요.";
        SetStatus(wrongSummaryStatus);
        StartCoroutine(RequestWrongSummary(summaryEndpoint, token, JsonUtility.ToJson(payload)));
    }

    private System.Collections.IEnumerator RequestWrongSummary(string endpoint, string token, string json)
    {
        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 60;
            yield return request.SendWebRequest();

            summarizingWrongs = false;
            if (request.result != UnityWebRequest.Result.Success)
                wrongSummaryStatus = "AI 요약에 실패했어요. 서버 주소와 연결 상태를 확인해 주세요.";
            else
            {
                AiWrongSummaryResponse response = null;
                try { response = JsonUtility.FromJson<AiWrongSummaryResponse>(request.downloadHandler.text); }
                catch (Exception e) { Debug.LogWarning("Could not parse AI wrong-answer summary: " + e.Message); }
                wrongSummaries.Clear();
                if (response != null && response.summaries != null)
                    wrongSummaries.AddRange(response.summaries.Where(item => item != null && !string.IsNullOrWhiteSpace(item.subject)));
                wrongSummaryStatus = wrongSummaries.Count > 0 ? "과목별 오답 요약을 업데이트했어요." : "서버 응답에서 과목별 요약을 찾지 못했어요.";
            }

            if (screen == Screen.Incorrect) ShowIncorrectScreen();
        }
    }

    private void ShowLearningArea()
    {
        string rangeLabel = string.IsNullOrEmpty(area) ? "전체 영역" : area;
        BeginScreen(Screen.Learning, subject + " · " + rangeLabel, "저장한 자료를 바탕으로 AI가 문제를 만들어요.");
        List<StudyEntry> rows = data.entries.Where(e => e.subject == subject && (string.IsNullOrEmpty(area) || e.area == area)).ToList();
        if (rows.Count == 0)
        {
            CreateText(content, "이 범위에 저장된 학습 내용이 없어요.", 25, ink, FontStyle.Bold);
            CreateButton(content, "내용 입력하기", true, () => OpenSubjectPicker(Flow.Input));
            return;
        }
        CreateText(content, "출제에 사용할 자료 " + rows.Count + "개", 24, ink, FontStyle.Bold);
        foreach (StudyEntry row in rows) CreateText(content, EntryHeading(row) + "\n" + Preview(row), 20, muted);

        gradeField = CreateInput("학년", "예: 중학교 2학년", false, PlayerPrefs.GetString("study_grade", ""));
        CreateText(content, "학년과 저장한 자료를 바탕으로 난이도와 문제 구성을 정합니다.", 21, muted);
        CreateButton(content, "AI로 문제 만들기", true, ShowQuizSetup);
    }

    private void ShowQuizSetup()
    {
        if (gradeField != null)
        {
            PlayerPrefs.SetString("study_grade", gradeField.text.Trim());
            PlayerPrefs.Save();
        }
        gradeField = null;
        selectedDifficulty = PlayerPrefs.GetString("quiz_difficulty", selectedDifficulty);
        multipleChoiceCountText = PlayerPrefs.GetString("quiz_multiple_choice_count", multipleChoiceCountText);
        essayCountText = PlayerPrefs.GetString("quiz_essay_count", essayCountText);
        shortAnswerCountText = PlayerPrefs.GetString("quiz_short_answer_count", shortAnswerCountText);

        BeginScreen(Screen.QuizSetup, "AI 문제 설정", "난이도와 문제 유형별 개수를 선택해 주세요.");
        CreateText(content, "난이도", 25, ink, FontStyle.Bold);
        GameObject difficultyRow = new GameObject("Difficulty choices", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        difficultyRow.transform.SetParent(content, false);
        HorizontalLayoutGroup difficultyLayout = difficultyRow.GetComponent<HorizontalLayoutGroup>();
        difficultyLayout.spacing = 12;
        difficultyLayout.childControlWidth = true;
        difficultyLayout.childControlHeight = true;
        difficultyLayout.childForceExpandWidth = true;
        difficultyLayout.childForceExpandHeight = false;
        difficultyRow.GetComponent<LayoutElement>().preferredHeight = 108;
        CreateDifficultyButton(difficultyRow.transform, "하", "기본 개념", selectedDifficulty == "하");
        CreateDifficultyButton(difficultyRow.transform, "중", "개념 적용", selectedDifficulty == "중");
        CreateDifficultyButton(difficultyRow.transform, "상", "응용·추론", selectedDifficulty == "상");
        CreateText(content, "하: 핵심 개념 확인   중: 개념 적용   상: 여러 개념을 활용하는 응용 문제", 19, muted);

        CreateText(content, "문제 수", 25, ink, FontStyle.Bold);
        multipleChoiceCountField = CreateInput("객관식", "개수 (0~20)", false, multipleChoiceCountText);
        essayCountField = CreateInput("서술형", "개수 (0~20)", false, essayCountText);
        shortAnswerCountField = CreateInput("주관식", "개수 (0~20)", false, shortAnswerCountText);
        ConfigureCountField(multipleChoiceCountField, value => multipleChoiceCountText = value);
        ConfigureCountField(essayCountField, value => essayCountText = value);
        ConfigureCountField(shortAnswerCountField, value => shortAnswerCountText = value);
        CreateText(content, "각 유형은 0개로 설정할 수 있고, 전체 문제는 최대 20개입니다.", 19, muted);
        CreateButton(content, generatingQuestions ? "문제 생성 중…" : "문제 생성하기", true, GenerateAiQuestions);
        AddStatus();
    }

    private void CreateDifficultyButton(Transform parent, string value, string description, bool selected)
    {
        Button button = CreateButton(parent, value, selected, () =>
        {
            selectedDifficulty = value;
            SaveQuizSettings();
            ShowQuizSetup();
        });
        Text descriptionText = CreateText(button.transform, description, 17, selected ? white : muted, FontStyle.Normal, TextAnchor.LowerCenter);
        RectTransform descriptionRect = descriptionText.rectTransform;
        descriptionRect.anchorMin = new Vector2(0.04f, 0.08f);
        descriptionRect.anchorMax = new Vector2(0.96f, 0.4f);
        descriptionRect.offsetMin = Vector2.zero;
        descriptionRect.offsetMax = Vector2.zero;
        descriptionText.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    private void ConfigureCountField(InputField field, UnityEngine.Events.UnityAction<string> onValueChanged)
    {
        field.contentType = InputField.ContentType.IntegerNumber;
        field.characterLimit = 2;
        field.onValueChanged.AddListener(onValueChanged);
    }

    private void SaveQuizSettings()
    {
        if (multipleChoiceCountField != null) multipleChoiceCountText = multipleChoiceCountField.text;
        if (essayCountField != null) essayCountText = essayCountField.text;
        if (shortAnswerCountField != null) shortAnswerCountText = shortAnswerCountField.text;
        PlayerPrefs.SetString("quiz_difficulty", selectedDifficulty);
        PlayerPrefs.SetString("quiz_multiple_choice_count", multipleChoiceCountText);
        PlayerPrefs.SetString("quiz_essay_count", essayCountText);
        PlayerPrefs.SetString("quiz_short_answer_count", shortAnswerCountText);
        PlayerPrefs.Save();
    }

    private void GenerateAiQuestions()
    {
        if (generatingQuestions) return;
        redoMode = false;
        string endpoint = (aiServerEndpoint ?? "").Trim();
        string serverToken = (aiServerAccessToken ?? "").Trim();
        string grade = PlayerPrefs.GetString("study_grade", "").Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("AI 서버 설정을 확인해 주세요.");
            return;
        }

        if (string.IsNullOrEmpty(grade))
        {
            SetStatus("학생의 학년을 입력해 주세요. 학년에 맞춰 문제 난이도를 조절합니다.");
            return;
        }

        int multipleChoiceCount;
        int essayCount;
        int shortAnswerCount;
        string multipleChoiceText = multipleChoiceCountField != null ? multipleChoiceCountField.text : multipleChoiceCountText;
        string essayText = essayCountField != null ? essayCountField.text : essayCountText;
        string shortAnswerText = shortAnswerCountField != null ? shortAnswerCountField.text : shortAnswerCountText;
        if (!int.TryParse(multipleChoiceText, out multipleChoiceCount) || multipleChoiceCount < 0 ||
            !int.TryParse(essayText, out essayCount) || essayCount < 0 ||
            !int.TryParse(shortAnswerText, out shortAnswerCount) || shortAnswerCount < 0)
        {
            SetStatus("문제 수를 0 이상의 숫자로 입력해 주세요.");
            return;
        }

        int questionCount = multipleChoiceCount + essayCount + shortAnswerCount;
        if (questionCount == 0 || questionCount > 20)
        {
            SetStatus("문제 수는 총 1개 이상 20개 이하로 설정해 주세요.");
            return;
        }

        PlayerPrefs.SetString("study_grade", grade);
        SaveQuizSettings();

        quizMaterials = data.entries.Where(e => e.subject == subject && (string.IsNullOrEmpty(area) || e.area == area)).ToList();
        if (quizMaterials.Count == 0)
        {
            SetStatus("선택한 과목과 영역에 저장된 학습 자료가 없어요. 자료를 먼저 추가해 주세요.");
            return;
        }
        if (quizMaterials.Count > 100)
        {
            SetStatus("AI 문제 생성에는 학습 자료를 최대 100개까지 사용할 수 있어요.");
            return;
        }

        AiQuizRequest payload = new AiQuizRequest
        {
            subject = subject,
            area = string.IsNullOrEmpty(area) ? "전체 영역" : area,
            grade = grade,
            difficulty = selectedDifficulty,
            questionCount = questionCount,
            multipleChoiceCount = multipleChoiceCount,
            essayCount = essayCount,
            shortAnswerCount = shortAnswerCount,
            instructions = "학생의 학년, 선택 난이도, 과목과 영역에 맞춰 문제를 출제하세요. 요청된 객관식·서술형·주관식 개수를 정확히 지키세요. 객관식은 보기 4개와 0부터 시작하는 answerIndex를 사용하고, 서술형과 주관식은 options를 빈 배열, answerIndex를 -1로 설정하며 모범 답안을 answer에 작성하세요. 서술형은 채점 기준을 gradingCriteria에 구체적으로 넣으세요. 저장 자료를 중심으로 출제하고, 과학·역사/사회 메모의 단원명과 형광펜 표시 내용을 우선 반영하세요. 국어·영어 문법은 메모의 문법 개념을 이해하는 문제를 만드세요. 과목의 일반 지식은 학년과 난이도에 맞게 활용하되 사실을 꾸며내지 마세요. 각 문제에 해설, 학습 주제 type, 가능한 경우 세부 영역과 출처 단원명을 넣으세요.",
            materials = quizMaterials.Select(entry => new AiMaterial
            {
                area = entry.area,
                kind = entry.kind,
                title = entry.title,
                content = entry.content,
                highlights = entry.highlights ?? new List<string>()
            }).ToList()
        };

        generatingQuestions = true;
        SetStatus("저장한 자료를 AI 서버로 보내 문제를 만들고 있어요.");
        StartCoroutine(RequestAiQuestions(endpoint, serverToken, JsonUtility.ToJson(payload)));
    }

    private System.Collections.IEnumerator RequestAiQuestions(string endpoint, string serverToken, string json)
    {
        AiQuizRequest requested = JsonUtility.FromJson<AiQuizRequest>(json);
        byte[] body = Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            if (!string.IsNullOrEmpty(serverToken)) request.SetRequestHeader("Authorization", "Bearer " + serverToken);
            request.timeout = 60;
            yield return request.SendWebRequest();

            generatingQuestions = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    SetStatus("AI 서버에 연결하지 못했어요. 주소와 서버 상태를 확인해 주세요. (" + request.error + ")");
                }
                else
                {
                    AiErrorResponse errorResponse = null;
                    try { errorResponse = JsonUtility.FromJson<AiErrorResponse>(request.downloadHandler.text); }
                    catch (Exception e) { Debug.LogWarning("Could not parse AI server error: " + e.Message); }
                    string detail = errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.error)
                        ? errorResponse.error
                        : request.error;
                    SetStatus("AI 문제 생성 요청을 처리하지 못했어요. (" + detail + ")");
                }
                yield break;
            }

            AiQuizResponse response = null;
            try { response = JsonUtility.FromJson<AiQuizResponse>(request.downloadHandler.text); }
            catch (Exception e) { Debug.LogWarning("Could not parse AI questions: " + e.Message); }
            quizQuestions.Clear();
            if (response != null && response.questions != null)
                quizQuestions.AddRange(response.questions.Where(IsValidGeneratedQuestion));

            int generatedMultipleChoice = quizQuestions.Count(IsMultipleChoice);
            int generatedEssay = quizQuestions.Count(q => q.format == "essay");
            int generatedShortAnswer = quizQuestions.Count(q => q.format == "short_answer");
            if (requested == null || quizQuestions.Count != requested.questionCount ||
                generatedMultipleChoice != requested.multipleChoiceCount ||
                generatedEssay != requested.essayCount || generatedShortAnswer != requested.shortAnswerCount)
            {
                quizQuestions.Clear();
                SetStatus("요청한 난이도와 유형별 개수에 맞는 문제를 받지 못했어요. 다시 생성해 주세요.");
                yield break;
            }

            quizIndex = 0;
            quizCorrectCount = 0;
            selectedAnswer = -1;
            writtenAnswer = "";
            writtenAnswerReviewed = false;
            writtenAnswerCorrect = false;
            writtenFeedback = "";
            gradingWrittenAnswer = false;
            ShowQuizQuestion();
        }
    }

    private bool IsMultipleChoice(AiQuestion question)
    {
        return question != null && (string.IsNullOrEmpty(question.format) || question.format == "multiple_choice");
    }

    private bool IsValidGeneratedQuestion(AiQuestion question)
    {
        if (question == null || string.IsNullOrWhiteSpace(question.question)) return false;
        if (IsMultipleChoice(question))
            return question.options != null && question.options.Count == 4 && question.answerIndex >= 0 && question.answerIndex < question.options.Count;
        if (question.format != "short_answer" && question.format != "essay") return false;
        if (string.IsNullOrWhiteSpace(question.answer)) return false;
        return question.format != "essay" || (question.gradingCriteria != null && question.gradingCriteria.Count > 0);
    }

    private void CreateQuizQuestionPaper(AiQuestion question, string formatLabel)
    {
        GameObject paper = new GameObject("Exam question", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        paper.transform.SetParent(content, false);
        Image background = paper.GetComponent<Image>();
        background.sprite = roundedSprite;
        background.type = Image.Type.Sliced;
        background.color = paperColor;
        LayoutElement paperSize = paper.GetComponent<LayoutElement>();
        paperSize.minHeight = 150;
        paperSize.flexibleWidth = 1;
        VerticalLayoutGroup paperLayout = paper.GetComponent<VerticalLayoutGroup>();
        paperLayout.padding = new RectOffset(22, 22, 18, 20);
        paperLayout.spacing = 10;
        paperLayout.childControlWidth = true;
        paperLayout.childControlHeight = true;
        paperLayout.childForceExpandWidth = true;
        paperLayout.childForceExpandHeight = false;

        string metadata = "문항 " + (quizIndex + 1).ToString("00") + "   ·   " + formatLabel;
        if (!string.IsNullOrEmpty(question.type)) metadata += "   ·   " + question.type;
        CreateText(paper.transform, metadata, 18, muted, FontStyle.Bold);

        GameObject rule = new GameObject("Exam paper rule", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rule.transform.SetParent(paper.transform, false);
        rule.GetComponent<Image>().color = lineColor;
        rule.GetComponent<Image>().raycastTarget = false;
        LayoutElement ruleSize = rule.GetComponent<LayoutElement>();
        ruleSize.minHeight = 2;
        ruleSize.preferredHeight = 2;
        ruleSize.flexibleWidth = 1;

        Text questionText = CreateText(paper.transform, question.question, 27, ink, FontStyle.Bold, TextAnchor.UpperLeft);
        questionText.alignment = TextAnchor.UpperLeft;
    }

    private void ShowQuizQuestion()
    {
        if (quizIndex >= quizQuestions.Count)
        {
            ShowQuizComplete();
            return;
        }

        AiQuestion question = quizQuestions[quizIndex];
        string quizTitle = redoMode ? "오답 고치기 " : "문제 ";
        string questionSubject = redoMode ? redoWrongAnswers[quizIndex].subject : subject;
        string questionArea = redoMode ? redoWrongAnswers[quizIndex].area : string.IsNullOrEmpty(area) ? "전체 영역" : area;
        BeginScreen(Screen.Quiz, quizTitle + (quizIndex + 1) + " / " + quizQuestions.Count, questionSubject + " · " + questionArea);
        writtenAnswerField = null;
        string formatLabel = IsMultipleChoice(question) ? "객관식" : question.format == "essay" ? "서술형" : "주관식";
        CreateQuizQuestionPaper(question, formatLabel);
        if (IsMultipleChoice(question))
        {
            for (int i = 0; i < question.options.Count; i++)
            {
                int optionIndex = i;
                string option = question.options[i];
                string optionMarker = i < OptionMarkers.Length ? OptionMarkers[i] : (i + 1) + ".";
                if (selectedAnswer < 0)
                {
                    Button optionButton = CreateButton(content, optionMarker + "  " + option, false, () => AnswerQuestion(optionIndex));
                    Text optionText = optionButton.GetComponentInChildren<Text>();
                    if (optionText != null) optionText.alignment = TextAnchor.MiddleLeft;
                }
                else
                {
                    string marker = i == question.answerIndex ? " ✓" : i == selectedAnswer ? " ✕" : "";
                    CreateText(content, optionMarker + "  " + option + marker, 23, i == question.answerIndex ? blue : muted, FontStyle.Bold);
                }
            }

            if (selectedAnswer >= 0)
            {
                string correctMarker = question.answerIndex < OptionMarkers.Length ? OptionMarkers[question.answerIndex] : (question.answerIndex + 1) + ".";
                string feedback = selectedAnswer == question.answerIndex ? "정답입니다." : "다시 확인해 보세요. 정답은 " + correctMarker + "입니다.";
                CreateText(content, feedback, 25, ink, FontStyle.Bold);
                if (!string.IsNullOrEmpty(question.explanation)) CreateText(content, question.explanation, 22, muted);
                CreateButton(content, quizIndex + 1 == quizQuestions.Count ? "결과 보기" : "다음 문제", true, NextQuestion);
            }
            return;
        }

        bool essay = question.format == "essay";
        writtenAnswerField = CreateInput(essay ? "답안 작성" : "답 입력", essay ? "풀이 과정을 적어 보세요." : "답을 입력하세요.", essay, writtenAnswer, essay);
        if (!writtenAnswerReviewed)
        {
            if (gradingWrittenAnswer)
            {
                AddStatus("AI가 답안을 채점하고 있어요.");
            }
            else
            {
                AddStatus();
                CreateButton(content, "AI로 답안 채점하기", true, GradeWrittenAnswer);
            }
            return;
        }

        CreateText(content, writtenAnswerCorrect ? "AI 채점 결과: 정답이에요." : "AI 채점 결과: 다시 확인해 보세요.", 24, ink, FontStyle.Bold);
        if (!string.IsNullOrEmpty(writtenFeedback)) CreateText(content, writtenFeedback, 22, muted);
        CreateText(content, "모범 답안\n" + question.answer, 23, ink, FontStyle.Bold);
        if (question.gradingCriteria != null)
            foreach (string criterion in question.gradingCriteria)
                CreateText(content, "• " + criterion, 20, muted);
        if (!string.IsNullOrEmpty(question.explanation)) CreateText(content, question.explanation, 21, muted);
        CreateButton(content, quizIndex + 1 == quizQuestions.Count ? "결과 보기" : "다음 문제", true, NextQuestion);
    }

    private void GradeWrittenAnswer()
    {
        if (gradingWrittenAnswer || writtenAnswerReviewed || quizIndex >= quizQuestions.Count) return;
        writtenAnswer = writtenAnswerField != null ? writtenAnswerField.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(writtenAnswer))
        {
            SetStatus("답안을 입력한 뒤 채점해 주세요.");
            return;
        }

        string endpoint = (aiServerEndpoint ?? "").Trim();
        string endpointBase = endpoint.EndsWith("/quiz", StringComparison.OrdinalIgnoreCase)
            ? endpoint.Substring(0, endpoint.Length - "/quiz".Length).TrimEnd('/')
            : endpoint.TrimEnd('/');
        string gradeEndpoint = endpointBase + "/grade-answer";
        if (!Uri.TryCreate(gradeEndpoint, UriKind.Absolute, out Uri gradeUri) ||
            (gradeUri.Scheme != Uri.UriSchemeHttp && gradeUri.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("AI 채점 서버 설정을 확인해 주세요.");
            return;
        }

        AiQuestion question = quizQuestions[quizIndex];
        AiGradeRequest payload = new AiGradeRequest
        {
            grade = PlayerPrefs.GetString("study_grade", "중학교"),
            format = question.format,
            question = question.question,
            studentAnswer = writtenAnswer,
            expectedAnswer = question.answer,
            gradingCriteria = question.gradingCriteria ?? new List<string>()
        };
        gradingWrittenAnswer = true;
        ShowQuizQuestion();
        StartCoroutine(RequestAiWrittenAnswerGrade(gradeEndpoint, (aiServerAccessToken ?? "").Trim(), JsonUtility.ToJson(payload), quizIndex));
    }

    private System.Collections.IEnumerator RequestAiWrittenAnswerGrade(string endpoint, string token, string json, int questionIndex)
    {
        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 60;
            yield return request.SendWebRequest();

            gradingWrittenAnswer = false;
            if (screen != Screen.Quiz || quizIndex != questionIndex) yield break;
            if (request.result != UnityWebRequest.Result.Success)
            {
                SetStatus("AI 채점에 실패했어요. 연결 상태를 확인하고 다시 시도해 주세요. (" + request.error + ")");
                yield break;
            }

            AiGradeResponse response = null;
            try { response = JsonUtility.FromJson<AiGradeResponse>(request.downloadHandler.text); }
            catch (Exception e) { Debug.LogWarning("Could not parse AI written-answer grade: " + e.Message); }
            if (response == null || string.IsNullOrWhiteSpace(response.feedback))
            {
                SetStatus("AI 채점 결과를 읽지 못했어요. 다시 시도해 주세요.");
                yield break;
            }

            CompleteWrittenGrade(response);
        }
    }

    private void AnswerQuestion(int optionIndex)
    {
        if (selectedAnswer >= 0 || quizIndex >= quizQuestions.Count) return;
        selectedAnswer = optionIndex;
        AiQuestion question = quizQuestions[quizIndex];
        bool correct = optionIndex == question.answerIndex;
        if (correct) quizCorrectCount++;

        if (redoMode)
        {
            WrongAnswer original = redoWrongAnswers[quizIndex];
            if (correct)
            {
                if (data.wrongAnswers.Remove(original)) redoFixedCount++;
            }
            else
            {
                original.chosenAnswer = question.options[optionIndex];
                original.date = DateTime.Now.ToString("yyyy-MM-dd");
            }
            wrongSummaries.Clear();
            wrongSummaryStatus = "";
            SaveData();
        }
        else if (!correct)
        {
            StudyEntry source = quizMaterials.FirstOrDefault(entry => entry.title == question.sourceTitle);
            data.wrongAnswers.Add(new WrongAnswer
            {
                subject = subject,
                area = !string.IsNullOrEmpty(question.area) ? question.area : source != null ? source.area : string.IsNullOrEmpty(area) ? "전체 영역" : area,
                type = !string.IsNullOrEmpty(question.type) ? question.type : source != null ? source.kind : "개념",
                format = question.format,
                question = question.question,
                chosenAnswer = question.options[optionIndex],
                correctAnswer = question.options[question.answerIndex],
                explanation = question.explanation,
                sourceTitle = question.sourceTitle,
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                options = new List<string>(question.options),
                gradingCriteria = question.gradingCriteria ?? new List<string>(),
                correctIndex = question.answerIndex
            });
            wrongSummaries.Clear();
            wrongSummaryStatus = "";
            SaveData();
        }
        ShowQuizQuestion();
    }

    private void CompleteWrittenGrade(AiGradeResponse grade)
    {
        if (writtenAnswerReviewed || quizIndex >= quizQuestions.Count) return;
        writtenAnswerReviewed = true;
        writtenAnswerCorrect = grade.isCorrect;
        writtenFeedback = grade.feedback;
        AiQuestion question = quizQuestions[quizIndex];
        if (writtenAnswerCorrect) quizCorrectCount++;

        if (redoMode)
        {
            WrongAnswer original = redoWrongAnswers[quizIndex];
            if (writtenAnswerCorrect)
            {
                if (data.wrongAnswers.Remove(original)) redoFixedCount++;
            }
            else
            {
                original.chosenAnswer = writtenAnswer;
                original.explanation = writtenFeedback;
                original.date = DateTime.Now.ToString("yyyy-MM-dd");
            }
        }
        else if (!writtenAnswerCorrect)
        {
            StudyEntry source = quizMaterials.FirstOrDefault(entry => entry.title == question.sourceTitle);
            data.wrongAnswers.Add(new WrongAnswer
            {
                subject = subject,
                area = !string.IsNullOrEmpty(question.area) ? question.area : source != null ? source.area : string.IsNullOrEmpty(area) ? "전체 영역" : area,
                type = !string.IsNullOrEmpty(question.type) ? question.type : source != null ? source.kind : "개념",
                format = question.format,
                question = question.question,
                chosenAnswer = writtenAnswer,
                correctAnswer = question.answer,
                explanation = string.IsNullOrWhiteSpace(question.explanation)
                    ? writtenFeedback
                    : writtenFeedback + "\n" + question.explanation,
                sourceTitle = question.sourceTitle,
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                options = new List<string>(),
                gradingCriteria = question.gradingCriteria ?? new List<string>(),
                correctIndex = -1
            });
            wrongSummaries.Clear();
            wrongSummaryStatus = "";
        }

        SaveData();
        ShowQuizQuestion();
    }

    private void NextQuestion()
    {
        quizIndex++;
        selectedAnswer = -1;
        writtenAnswer = "";
        writtenAnswerReviewed = false;
        writtenAnswerCorrect = false;
        writtenFeedback = "";
        gradingWrittenAnswer = false;
        ShowQuizQuestion();
    }

    private void ShowQuizComplete()
    {
        string resultScope = subject + " · " + (string.IsNullOrEmpty(area) ? "전체 영역" : area);
        if (redoMode)
        {
            List<string> redoSubjects = redoWrongAnswers.Select(item => item.subject).Distinct().ToList();
            resultScope = redoSubjects.Count > 1 ? "여러 과목 오답" : redoSubjects.Count == 1 ? redoSubjects[0] + " 오답" : "오답";
        }
        BeginScreen(Screen.Quiz, redoMode ? "오답 고치기 완료" : "학습 완료", resultScope);
        if (redoMode)
        {
            CreateText(content, "다시 맞힌 오답 " + redoFixedCount + "개", 29, ink, FontStyle.Bold);
            CreateText(content, "남은 오답 " + (data.wrongAnswers != null ? data.wrongAnswers.Count : 0) + "개", 22, muted);
        }
        else
        {
            CreateText(content, quizQuestions.Count + "문제 중 " + quizCorrectCount + "문제를 맞혔어요.", 29, ink, FontStyle.Bold);
            CreateText(content, "틀린 문제는 과목별 오답 목록에서 다시 볼 수 있어요.", 22, muted);
        }
        CreateButton(content, "틀린 문제 확인하기", true, ShowIncorrect);
        CreateButton(content, "다른 범위 학습하기", false, () => OpenSubjectPicker(Flow.Learning));
    }
}

public class FastScrollRect : ScrollRect
{
    public float dragSpeedMultiplier = 1.8f;
    private Vector2 dragOrigin;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        dragOrigin = eventData.position;
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        Vector2 originalPosition = eventData.position;
        float multiplier = Mathf.Max(1f, dragSpeedMultiplier);
        eventData.position = dragOrigin + (originalPosition - dragOrigin) * multiplier;
        base.OnDrag(eventData);
        eventData.position = originalPosition;
    }
}

public class LongPressGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action onLongPress;
    public float holdDuration = 0.7f;

    private bool pointerHeld;
    private bool invoked;
    private float pressStartedAt;
    private int pointerId;
    private Vector2 pressPosition;

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        invoked = false;
        pressStartedAt = Time.unscaledTime;
        pointerId = eventData.pointerId;
        pressPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerHeld = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerHeld = false;
    }

    private bool PointerMoved()
    {
        Vector2 pointerPosition;
        if (pointerId < 0)
        {
            pointerPosition = Input.mousePosition;
        }
        else
        {
            bool foundTouch = false;
            pointerPosition = pressPosition;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != pointerId) continue;
                pointerPosition = touch.position;
                foundTouch = true;
                break;
            }
            if (!foundTouch) return true;
        }

        float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;
        return (pointerPosition - pressPosition).sqrMagnitude > threshold * threshold;
    }

    private void Update()
    {
        if (pointerHeld && PointerMoved()) pointerHeld = false;
        if (!pointerHeld || invoked || Time.unscaledTime - pressStartedAt < holdDuration) return;
        invoked = true;
        pointerHeld = false;
        onLongPress?.Invoke();
    }
}
