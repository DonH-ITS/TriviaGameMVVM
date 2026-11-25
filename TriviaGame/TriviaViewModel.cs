/*
================================================================================
TriviaViewModel - MVVM Overview
================================================================================

This ViewModel is part of a simple Trivia Game using .NET MAUI. It demonstrates
the MVVM (Model-View-ViewModel) pattern, where the ViewModel acts as the bridge
between the UI (View) and the data (Model).

Key concepts illustrated here:

1. Properties and INotifyPropertyChanged
   - Properties like ShowQuestion, Answers, EnabledButtons are "bindable".
   - When a property changes, the UI automatically updates if it binds to it.
   - OnPropertyChanged() triggers this update.

2. Commands
   - Commands like StartGameCommand, AnswerSelectedCommand, and NextQCommand
     are bound to buttons in the View.
   - When a button is clicked, the associated command calls a method in the
     ViewModel (StartGame, OnAnswerSelected, NextQuestion).

3. Events to communicate with the View
   - ShowAlert event is invoked to display pop-up messages in the UI.
   - MoveButton event is invoked to animate buttons without the ViewModel
     needing direct access to UI controls (still maintains MVVM separation).

4. Data fetching and processing
   - DownloadQuestions() fetches trivia questions from OpenTDB.
   - PrepareQuestion() sets up the current question, shuffles answers, and
     prepares button visibility.

5. Binding Example:
   - XAML binds Buttons' Text to Answers[i] and Visibility to ButtonVisible[i].
   - The Next button binds to NextButton property and NextQCommand.
   - Category/Difficulty pickers bind to SelectedCategory and SelectedDifficulty.

Summary:
- The ViewModel contains all game logic and data.
- The View (XAML page) simply binds to these properties and commands.
- MVVM ensures separation of concerns, testability, and maintainable UI code.
================================================================================
*/


using System.ComponentModel;
using System.Text.Json;
using System.Web;

namespace TriviaGame
{
    public class TriviaViewModel : INotifyPropertyChanged
    {
        // ======================
        // Private fields
        // ======================
        // Stores the downloaded trivia questions and answers
        private TriviaResponse? _triviaResponse;
        private HttpClient _httpClient;

        // Controls visibility of UI elements
        private bool _showQuestion = false, _showStartBtn = false, _enabledButtons = false;
        private int _questionNo = 0;
        private string _correctAnswer = "";
        private int _countCorrect = 0;
        private bool[] _buttonVisible = new bool[4] { true, true, true, true };
        private string _correctString = "";
        private bool _showCorrectLabel = false;
        private int _numberOfQuestions = 5;
        private bool _showNextBtn = false;
        private int _selectedButton = -1;
        private bool _showFinish = false;

        // ======================
        // Events
        // ======================
        // Used to trigger alerts in the View
        public event Action<string, string, string>? ShowAlert;

        // Event to notify the View to animate buttons
        public event Action<int, bool>? MoveButton;

        // ======================
        // Commands
        // ======================
        // Commands bind to buttons in the UI
        public Command StartGameCommand { get; }
        public Command<string> AnswerSelectedCommand { get; }
        public Command NextQCommand { get; }

        // ======================
        // Lists for categories and difficulty
        // ======================
        public List<TriviaCategory> Categories { get; }
        public List<string> Difficulties { get; }

        // ======================
        // Selected category/difficulty and answers
        // ======================
        public TriviaCategory SelectedCategory { get; set; }
        public string SelectedDifficulty { get; set; }
        public string[] Answers { get; set; } = new string[4];

        // ======================
        // Properties for UI binding
        // ======================

        // Displays Correct/Incorrect text
        public string CorrectString
        {
            get => _correctString;
            set
            {
                _correctString = value;
                OnPropertyChanged();
            }
        }

        // Controls visibility of the Next button
        public bool ShowNextBtn
        {
            get => _showNextBtn;
            set
            {
                _showNextBtn = value;
                OnPropertyChanged();
            }
        }

        // Determines whether the activity indicator is shown
        public bool ShowActivity
        {
            get
            {
                if (!ShowQuestion && !ShowStartBtn && !ShowFinish)
                    return true;
                else
                    return false;
            }
        }

        // Controls whether answer buttons are clickable
        public bool EnabledButtons
        {
            get => _enabledButtons;
            set
            {
                if (_enabledButtons != value) {
                    _enabledButtons = value;
                    OnPropertyChanged();
                }
            }
        }

        // Controls visibility of the correct/incorrect label
        public bool ShowCorrectLabel
        {
            get => _showCorrectLabel;
            set
            {
                if (_showCorrectLabel != value) {
                    _showCorrectLabel = value;
                    OnPropertyChanged();
                }
            }
        }

        // Array controlling visibility of each answer button
        public bool[] ButtonVisible
        {
            get => _buttonVisible;
        }

        // Controls whether the end-of-game screen is visible
        public bool ShowFinish
        {
            get => _showFinish;
            set
            {
                _showFinish = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FinishString));
            }
        }

        // Text summarizing the number of correct answers
        public string FinishString
        {
            get
            {
                if (_triviaResponse != null)
                    return _countCorrect + " correct out of " + _triviaResponse.results.Count;
                else
                    return "";
            }
        }

        // Controls visibility of the Start button
        public bool ShowStartBtn
        {
            get => _showStartBtn;
            set
            {
                if (_showStartBtn != value) {
                    _showStartBtn = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowActivity));
                }
            }
        }

        // Controls visibility of the question
        public bool ShowQuestion
        {
            get => _showQuestion;
            set
            {
                if (_showQuestion != value) {
                    _showQuestion = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowActivity));
                }
            }
        }

        // Tracks which question number is currently displayed
        public int QuestionNo
        {
            get => _questionNo;
            set
            {
                if (_questionNo != value) {
                    if (_triviaResponse != null && value > 0 && value <= _triviaResponse.results.Count) {
                        _questionNo = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(CurrentQuestion));
                        OnPropertyChanged(nameof(NextButton));
                    }
                }
            }
        }

        // Number of questions in the game (bound to Slider)
        public double NumberOfQuestions
        {
            get => _numberOfQuestions;
            set
            {
                // Round to nearest integer to make discrete steps
                int newValue = (int)Math.Round(value);
                newValue = Math.Clamp(newValue, 5, 20);
                if (_numberOfQuestions != newValue) {
                    _numberOfQuestions = newValue;
                    OnPropertyChanged();
                }
            }
        }

        // Text for the Next/End Game/New Game button
        public string NextButton
        {
            get
            {
                if (_triviaResponse != null && QuestionNo < _triviaResponse.results.Count)
                    return "Next Question";
                else if (!ShowFinish)
                    return "End Game";
                else
                    return "New Game";
            }
        }

        // Returns the current question object for binding
        public Question? CurrentQuestion
        {
            get
            {
                if (_triviaResponse != null && _triviaResponse.results.Count > 0 && QuestionNo <= _triviaResponse.results.Count) {
                    // Decode HTML entities for display
                    _triviaResponse.results[QuestionNo - 1].question = HttpUtility.HtmlDecode(_triviaResponse.results[QuestionNo - 1].question);
                    _triviaResponse.results[QuestionNo - 1].category = HttpUtility.HtmlDecode(_triviaResponse.results[QuestionNo - 1].category);
                    return _triviaResponse.results[QuestionNo - 1];
                }
                else
                    return null;
            }
        }

        // ======================
        // Constructor
        // ======================
        public TriviaViewModel() {
            _httpClient = new HttpClient();
            ShowStartBtn = true;

            // Commands wired to buttons in the view
            StartGameCommand = new Command(StartGame);
            AnswerSelectedCommand = new Command<string>(OnAnswerSelected);
            NextQCommand = new Command(NextQuestion);

            // Initialize category and difficulty lists for Pickers
            Categories = new List<TriviaCategory>
            {
                new TriviaCategory(0, "Any Category"),
                new TriviaCategory(9, "General Knowledge"),
                new TriviaCategory(10, "Entertainment: Books"),
                new TriviaCategory(11, "Entertainment: Film"),
                new TriviaCategory(12, "Entertainment: Music"),
                new TriviaCategory(15, "Entertainment: Video Games"),
                new TriviaCategory(17, "Science & Nature"),
                new TriviaCategory(18, "Science: Computers"),
                new TriviaCategory(19, "Science: Mathematics"),
                new TriviaCategory(20, "Mythology"),
                new TriviaCategory(21, "Sports"),
                new TriviaCategory(22, "Geography"),
                new TriviaCategory(23, "History")
            };
            Difficulties = new List<string> { "Any", "Easy", "Medium", "Hard" };
            SelectedCategory = Categories[0];
            SelectedDifficulty = Difficulties[0];
        }

        // ======================
        // Game logic methods
        // ======================

        // Starts the game: resets counters and downloads questions
        private async void StartGame() {
            ShowStartBtn = false;
            _countCorrect = 0;
            ShowFinish = false;
            ShowNextBtn = false;
            QuestionNo = 1;
            await DownloadQuestions();
        }

        // Handles user selecting an answer
        private void OnAnswerSelected(string which) {
            _selectedButton = Convert.ToInt32(which);
            EnabledButtons = false;

            // Hide unselected buttons
            for (int i = 0; i < 4; ++i) {
                if (i != _selectedButton)
                    ButtonVisible[i] = false;
            }
            OnPropertyChanged(nameof(ButtonVisible));

            string selected = Answers[_selectedButton];

            // Check if answer is correct
            if (selected == _correctAnswer) {
                CorrectString = "Correct";
                ++_countCorrect;
                MoveButton?.Invoke(_selectedButton, true);

            }
            else {
                CorrectString = "Incorrect";
                MoveButton?.Invoke(_selectedButton, true);
            }
        }

        // Moves to the next question or ends the game
        public void NextQuestion() {
            if (QuestionNo < _triviaResponse?.results.Count) {
                MoveButton?.Invoke(_selectedButton, false);
                QuestionNo++;
                PrepareQuestion();
            }
            else if (!ShowFinish) {
                MoveButton?.Invoke(_selectedButton, false);
                ShowFinish = true;
                ShowQuestion = false;
                ShowCorrectLabel = false;
            }
            else {
                ShowFinish = false;
                _triviaResponse?.results.Clear();
                ShowStartBtn = true;
                ShowNextBtn = false;
            }
        }

        // ======================
        // Data fetching
        // ======================
        private async Task DownloadQuestions() {
            string url = MakeURL();
            try {
                var response = await _httpClient.GetAsync(url);
                if (response != null && response.IsSuccessStatusCode) {
                    string text = await response.Content.ReadAsStringAsync();
                    _triviaResponse = JsonSerializer.Deserialize<TriviaResponse>(text);
                }
            }
            catch {
                ShowStartBtn = true;
                ShowAlert?.Invoke("Error", "Error Downloading Questions", "OK");
                return;
            }
            QuestionNo = 1;
            PrepareQuestion();
            ShowQuestion = true;
        }

        // Builds the URL for the OpenTDB API based on user selection
        private string MakeURL() {
            string result = "https://opentdb.com/api.php?amount=";
            result += _numberOfQuestions.ToString();
            if (SelectedCategory.Id != 0) {
                result += "&category=" + SelectedCategory.Id;
            }
            if (SelectedDifficulty != "Any") {
                result += "&difficulty=" + SelectedDifficulty.ToLower();
            }

            return result;
        }

        // ======================
        // Answer management
        // ======================
        // Shuffles the 4 answer options randomly
        public void ShuffleAnswers() {
            var rnd = new Random();

            for (int i = Answers.Length - 1; i > 0; i--) {
                int j = rnd.Next(i + 1);
                (Answers[i], Answers[j]) = (Answers[j], Answers[i]);
            }

            OnPropertyChanged(nameof(Answers));
        }

        // Prepares the answers and visibility for the current question
        private void PrepareQuestion() {
            ShowCorrectLabel = false;
            ShowNextBtn = false;
            if (_triviaResponse == null) return;

            // True/False or multiple choice logic
            Answers[0] = HttpUtility.HtmlDecode(_triviaResponse.results[QuestionNo - 1].correct_answer);
            _correctAnswer = Answers[0];

            if (_triviaResponse.results[QuestionNo - 1].incorrect_answers.Count == 3) {
                for (int i = 0; i < 3; i++) {
                    Answers[i + 1] = HttpUtility.HtmlDecode(_triviaResponse.results[QuestionNo - 1].incorrect_answers[i]);
                }

                ShuffleAnswers();
                for (int i = 0; i < 4; ++i) {
                    ButtonVisible[i] = true;
                }
            }
            else {
                Answers[0] = "True";
                Answers[1] = "False";

                for (int i = 0; i < 2; ++i)
                    ButtonVisible[i] = true;
                for (int i = 2; i < 4; ++i)
                    ButtonVisible[i] = false;
            }

            OnPropertyChanged(nameof(ButtonVisible));
            EnabledButtons = true;
        }

        // ======================
        // INotifyPropertyChanged implementation
        // ======================
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

