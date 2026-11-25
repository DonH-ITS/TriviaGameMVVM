namespace TriviaGame
{
    public partial class MainPage : ContentPage
    {
        // Store reference to the ViewModel so we can subscribe to events
        private TriviaViewModel _triviaVModel;

        // List of UI buttons so we can access them by index from the ViewModel callback
        private List<Button> Buttons;

        // Stores the original colour of a button before animation so we can restore it
        private Color original = Colors.Red;

        public MainPage() {
            InitializeComponent();

            // Create ViewModel and attach to BindingContext so UI can use data binding
            _triviaVModel = new TriviaViewModel();
            Buttons = new() { Button0, Button1, Button2, Button3 };
            BindingContext = _triviaVModel;

            // Connect ViewModel alert event to UI alert function
            // ViewModel tells UI to show messages, but doesn't handle UI itself
            _triviaVModel.ShowAlert += async (title, message, button) =>
            {
                await DisplayAlertAsync(title, message, button);
            };

            // Subscribe to animation event from ViewModel
            // ViewModel just signals "button X was selected", UI performs animation here
            _triviaVModel.MoveButton += Moving;
        }

        // Handles animations when a user selects an answer
        // ViewModel tells us which button to animate and whether to centre it
        private async void Moving(int which, bool centre) {
            double width = ButtonGrid.Width;
            Button btn = Buttons[which];

            // --- When answer is selected (move to centre and animate) ---
            if (centre) {
                // Visual "pop" animation to highlight selection
                await btn.ScaleToAsync(1.3, 100);
                await btn.ScaleToAsync(1.0, 100);

                // Store and temporarily change colour while moving
                original = btn.BackgroundColor;
                btn.BackgroundColor = Color.FromArgb("#2196F3");

                // Calculate X offset so button moves to the centre of the grid
                double targetX = (width / 2) - (btn.X + btn.Width / 2);

                // Smooth movement animation with slight overshoot bounce
                await btn.TranslateToAsync(targetX, 0, 800, Easing.CubicOut);
                await btn.TranslateToAsync(targetX - 10, 0, 100, Easing.CubicIn);
                await btn.TranslateToAsync(targetX, 0, 100, Easing.CubicOut);

                // Fade in the "Correct"/"Incorrect" message
                CorrectLabel.Opacity = 0;
                _triviaVModel.ShowCorrectLabel = true;
                await CorrectLabel.FadeToAsync(0.5, 500);

                // Change colour based on correctness
                btn.BackgroundColor = _triviaVModel.CorrectString == "Correct"
                    ? Color.FromArgb("#4CAF50")
                    : Color.FromArgb("#F44336");

                await CorrectLabel.FadeToAsync(1, 500);

                // Reveal the Next button so user can move forward
                _triviaVModel.ShowNextBtn = true;
            }
            // --- Reset button when moving to next question ---
            else {
                // Move button back to original location and restore colour
                btn.TranslationX = 0;
                btn.BackgroundColor = original;
            }
        }
    }
}
