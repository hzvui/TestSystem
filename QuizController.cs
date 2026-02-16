using System;
using Unigine;

[Component(PropertyGuid = "c4cc469f6dd55932c141ed9b8dfd0f91a32b60b5")]
public class QuizController : Component
{
    [Serializable]
    public class QuizQuestion
    {
        [Parameter(Title = "Question Text")]
        public string Question = "Введите вопрос";

        [Parameter(Title = "Answer 1")]
        public string Answer1 = "Вариант ответа 1";
        [Parameter(Title = "Answer 2")]
        public string Answer2 = "Вариант ответа 2";
        [Parameter(Title = "Answer 3")]
        public string Answer3 = "Вариант ответа 3";
        [Parameter(Title = "Answer 4")]
        public string Answer4 = "Вариант ответа 4";

        [Parameter(Title = "Correct Answer (1-4)")]
        public int CorrectAnswerIndex = 1;
    }

    [ShowInEditor] private bool ShowQuizOnStart = true;
    [ShowInEditor] private int CorrectAnswersToPass = 3;

    [ShowInEditor]
    public QuizQuestion[] Questions = new QuizQuestion[0];

    [ShowInEditor][Parameter(Title = "Кнопка A")] private Node ans1node;
    [ShowInEditor][Parameter(Title = "Кнопка B")] private Node ans2node;
    [ShowInEditor][Parameter(Title = "Кнопка C")] private Node ans3node;
    [ShowInEditor][Parameter(Title = "Кнопка D")] private Node ans4node;

    [ShowInEditor]
    [Parameter(Title = "Порог нажатия по Z (м)")]
    public float ButtonPressThreshold = 0.02f;

    // Время задержки перед переходом к следующему вопросу (в секундах)
    [ShowInEditor]
    [Parameter(Title = "Задержка после ответа (с)")]
    public float DelayBeforeNextQuestion = 1.0f;

    // Внутренние данные
    private Node[] answerNodes;
    private float[] initialZPositions;
    private bool[] wasPressed;

    private Gui GUI;
    private ObjectGui ObjectGUIMesh;
    private WidgetWindow mainWindow;
    private WidgetLabel questionLabel;
    private WidgetLabel resultLabel; // только для промежуточного ✓/✗

    private int currentQuestionIndex = 0;
    private int correctAnswersCount = 0;
    private bool isQuizActive = false;
    private bool answerSelected = false;
    private float answerTimer = 0.0f;

    void Init()
    {
        ObjectGUIMesh = node as ObjectGui;
        if (ObjectGUIMesh == null)
        {
            Log.Error("QuizController must be attached to an ObjectGui node!");
            return;
        }

        if (Questions == null || Questions.Length == 0)
        {
            Log.Warning("QuizController: No questions defined.");
            return;
        }

        GUI = ObjectGUIMesh.GetGui();
        if (GUI == null)
        {
            Log.Error("Failed to get GUI instance from ObjectGui!");
            return;
        }

        answerNodes = new Node[] { ans1node, ans2node, ans3node, ans4node };
        initialZPositions = new float[4];
        wasPressed = new bool[4];

        for (int i = 0; i < 4; i++)
        {
            if (answerNodes[i] != null)
            {
                initialZPositions[i] = answerNodes[i].WorldPosition.z;
            }
        }

        CreateQuizGUI();

        if (ShowQuizOnStart)
            StartQuiz();
        else  
            HideQuiz();
    }
// === ДОБАВЬ ЭТО В ПОЛЯ КЛАССА ===
private WidgetLabel[] answerLabels = new WidgetLabel[4]; // Тексты ответов как подписи
// ================================

private void CreateQuizGUI()
{
    // Главное окно
    mainWindow = new WidgetWindow(GUI, "Тестирование");
    mainWindow.Width = ObjectGUIMesh.ScreenWidth;
    mainWindow.Height = ObjectGUIMesh.ScreenHeight;
    mainWindow.Arrange();
    GUI.AddChild(mainWindow, Gui.ALIGN_EXPAND); // окно центрируется, но контент — по сетке

    // === Вопрос: вверху слева ===
    questionLabel = new WidgetLabel(GUI);
    questionLabel.FontSize = 18;
    questionLabel.Width = 780;
    questionLabel.Height = 100;
    questionLabel.SetPosition(10, 10); // левый верхний угол
    questionLabel.FontWrap = 1;
    questionLabel.TextAlign = Gui.ALIGN_LEFT;
    mainWindow.AddChild(questionLabel);

    // === Результат (✓/✗) под вопросом ===
    resultLabel = new WidgetLabel(GUI);
    resultLabel.FontSize = 20;
    resultLabel.Width = 780;
    resultLabel.Height = 50;
    resultLabel.SetPosition(10, 120);
    resultLabel.TextAlign = Gui.ALIGN_LEFT;
    resultLabel.Hidden = true;
    mainWindow.AddChild(resultLabel);

    // === Ответы в сетке 2×2 ===
    // Размеры и позиции
    int labelWidth = 370;  // чуть меньше половины (800 / 2 = 400)
    int labelHeight = 80;
    int hMargin = 10;      // отступ от краёв и между колонками
    int vMargin = 180;     // отступ сверху под вопросом

    // A — верхний левый
    answerLabels[0] = CreateAnswerLabel(GUI, labelWidth, labelHeight);
    answerLabels[0].SetPosition(hMargin, vMargin);

    // B — верхний правый
    answerLabels[1] = CreateAnswerLabel(GUI, labelWidth, labelHeight);
    answerLabels[1].SetPosition(800 - hMargin - labelWidth, vMargin);

    // C — нижний левый
    answerLabels[2] = CreateAnswerLabel(GUI, labelWidth, labelHeight);
    answerLabels[2].SetPosition(hMargin, vMargin + labelHeight + 10);

    // D — нижний правый
    answerLabels[3] = CreateAnswerLabel(GUI, labelWidth, labelHeight);
    answerLabels[3].SetPosition(800 - hMargin - labelWidth, vMargin + labelHeight + 10);

    // Добавляем в окно
    for (int i = 0; i < 4; i++)
    {
        mainWindow.AddChild(answerLabels[i]);
    }
}

// Вспомогательный метод для создания стиля ответа
private WidgetLabel CreateAnswerLabel(Gui gui, int width, int height)
{
    var label = new WidgetLabel(gui);
    label.FontSize = 16;
    label.Width = width;
    label.Height = height;
    label.FontWrap = 1;
    label.TextAlign = Gui.ALIGN_LEFT;
    return label;
}

    public void StartQuiz()
    {
        currentQuestionIndex = 0;
        correctAnswersCount = 0;
        isQuizActive = true;
        answerSelected = false;
        answerTimer = 0.0f;
        Array.Clear(wasPressed, 0, wasPressed.Length);

        ShowCurrentQuestion();
        mainWindow.Hidden = false;
    }

private void ShowCurrentQuestion()
{
    if (currentQuestionIndex >= Questions.Length)
    {
        EndQuiz();
        return;
    }

    QuizQuestion q = Questions[currentQuestionIndex];
    answerSelected = false;
    resultLabel.Hidden = true;
    Array.Clear(wasPressed, 0, wasPressed.Length);

    // Вопрос
    questionLabel.Text = $"Вопрос {currentQuestionIndex + 1} из {Questions.Length}\n\n{q.Question}";

    // Варианты ответов
    answerLabels[0].Text = $"A. {q.Answer1}";
    answerLabels[1].Text = $"B. {q.Answer2}";
    answerLabels[2].Text = $"C. {q.Answer3}";
    answerLabels[3].Text = $"D. {q.Answer4}";
}

    void Update()
    {
        if (!isQuizActive) return;

        // Автоматический переход после задержки
        if (answerSelected)
        {
            answerTimer += Game.IFps; // Game.IFps = delta time
            if (answerTimer >= DelayBeforeNextQuestion)
            {
                currentQuestionIndex++;
                ShowCurrentQuestion();
            }
            return;
        }

        // Проверка нажатий на кнопки
        for (int i = 0; i < 4; i++)
        {
            if (answerNodes[i] == null) continue;

            float currentZ = answerNodes[i].WorldPosition.z;
            float initialZ = initialZPositions[i];
            bool isPressedNow = (initialZ - currentZ) >= ButtonPressThreshold;

            if (isPressedNow && !wasPressed[i])
            {
                wasPressed[i] = true;
                ProcessAnswer(i);
                break;
            }
            else if (!isPressedNow)
            {
                wasPressed[i] = false;
            }
        }
    }

    private void ProcessAnswer(int answerIndex)
    {
        if (answerSelected || currentQuestionIndex >= Questions.Length) return;

        answerSelected = true;
        answerTimer = 0.0f;

        QuizQuestion q = Questions[currentQuestionIndex];
        bool isCorrect = (answerIndex + 1) == q.CorrectAnswerIndex;

        if (isCorrect)
        {
            correctAnswersCount++;
            resultLabel.Text = "✓ Правильно!";
            resultLabel.FontColor = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
        }
        else
        {
            resultLabel.Text = "✗ Неправильно!";
            resultLabel.FontColor = new vec4(1.0f, 0.0f, 0.0f, 1.0f);
        }

        resultLabel.Hidden = false;
    }

    private void EndQuiz()
    {
        isQuizActive = false;
        bool passed = correctAnswersCount >= CorrectAnswersToPass;

        string finalMessage = passed
            ? $"✅ Тест пройден!\nПравильных ответов: {correctAnswersCount} из {Questions.Length}"
            : $"❌ Тест не пройден!\nПравильных ответов: {correctAnswersCount} из {Questions.Length}\nТребуется: {CorrectAnswersToPass}";

        questionLabel.Text = "Тест завершён";
        resultLabel.Text = finalMessage;
        resultLabel.FontColor = passed ? new vec4(0.0f, 1.0f, 0.0f, 1.0f) : new vec4(1.0f, 0.0f, 0.0f, 1.0f);
        resultLabel.Hidden = false;
    }

    public void HideQuiz()
    {
        if (mainWindow != null)
            mainWindow.Hidden = true;
    }

    public void ShowQuiz()
    {
        if (mainWindow != null)
            mainWindow.Hidden = false;
    }

    public bool IsQuizPassed()
    {
        return !isQuizActive && correctAnswersCount >= CorrectAnswersToPass;
    }

    void Shutdown()
    {
        if (GUI != null && mainWindow != null)
        {
            GUI.RemoveChild(mainWindow);
        }
    }
}