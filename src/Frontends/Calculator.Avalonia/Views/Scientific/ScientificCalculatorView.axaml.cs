using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Calculator.Avalonia.Controls;
using Calculator.Managed;

namespace Calculator.Avalonia.Views;

/// <summary>
/// The Scientific keypad, its trigonometry and function flyouts, and the angle
/// and F-E controls.
/// </summary>
/// <remarks>
/// The size class is measured from this control's own keypad bounds rather than
/// from the window. That is how the source application behaves — the thresholds
/// are about how much room the keypad got, not how large the window is — and it
/// keeps the shell out of scientific-specific layout entirely.
/// </remarks>
public partial class ScientificCalculatorView : UserControl, IShortcutPressedTarget
{
    public ScientificCalculatorView()
    {
        InitializeComponent();

        ScientificTrigFlyoutGrid.AddHandler(Button.ClickEvent, PopupCommand_OnClick);
        ScientificFunctionFlyoutGrid.AddHandler(Button.ClickEvent, PopupCommand_OnClick);
        ScientificInverseOperators.AddHandler(Button.ClickEvent, InverseCommand_OnClick);
        ScientificNumpadPanel.SizeChanged += (_, _) => UpdateSizeClass();

        UpdateSizeClass();
    }

    private CalculatorViewModel? ViewModel => DataContext as CalculatorViewModel;

    private void UpdateSizeClass()
    {
        var width = ScientificNumpadPanel.Bounds.Width;
        var height = ScientificNumpadPanel.Bounds.Height;
        var state = width >= 878 && height >= 851
            ? "scientificLarge"
            : width >= 527 && height >= 523
                ? "scientificMedium"
                : "scientificSmall";

        ScientificNumpadPanel.Classes.Set("scientificSmall", state == "scientificSmall");
        ScientificNumpadPanel.Classes.Set("scientificMedium", state == "scientificMedium");
        ScientificNumpadPanel.Classes.Set("scientificLarge", state == "scientificLarge");

        (ScientificTrigFlyoutGrid.Width, ScientificTrigFlyoutGrid.Height,
         ScientificFunctionFlyoutGrid.Width, ScientificFunctionFlyoutGrid.Height) = state switch
        {
            "scientificLarge" => (516d, 192d, 387d, 192d),
            "scientificMedium" => (480d, 144d, 360d, 144d),
            _ => (258d, 96d, 194d, 96d),
        };
    }

    private void PopupCommand_OnClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button)
        {
            return;
        }

        // The 2nd/hyp controls alter the currently displayed trig group and do
        // not invoke a calculator operation, so the source flyout stays open.
        // They are marked with a class rather than identified by comparing
        // command instances, which would break the moment the commands are
        // re-exposed by a scientific-specific view model.
        if (button.Classes.Contains("flyoutStateToggle"))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            ScientificTrigButton.Flyout?.Hide();
            ScientificFunctionButton.Flyout?.Hide();

            if (ViewModel is { } viewModel)
            {
                viewModel.IsTrigInverse = false;
                viewModel.IsTrigHyperbolic = false;
            }
        });
    }

    private void InverseCommand_OnClick(object? sender, RoutedEventArgs e)
    {
        // UWP unchecks 2nd after an inverse operator executes. Post the state
        // change so the button's calculator command completes first.
        if (e.Source is Button)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ViewModel is { } viewModel)
                {
                    viewModel.IsScientificInverse = false;
                }
            });
        }
    }

    public bool TrySetShortcutPressed(string shortcutId, bool isPressed)
    {
        var button = shortcutId switch
        {
            "clearButton" => ScientificClearButton,
            "clearEntryButton" => ScientificClearEntryButton,
            "decimalSeparatorButton" => ScientificDecimalButton,
            "divideButton" => ScientificDivideButton,
            "equalButton" => ScientificEqualsButton,
            "minusButton" => ScientificSubtractButton,
            "negateButton" => ScientificSignButton,
            "num0Button" => ScientificZeroButton,
            "num1Button" => ScientificOneButton,
            "num2Button" => ScientificTwoButton,
            "num3Button" => ScientificThreeButton,
            "num4Button" => ScientificFourButton,
            "num5Button" => ScientificFiveButton,
            "num6Button" => ScientificSixButton,
            "num7Button" => ScientificSevenButton,
            "num8Button" => ScientificEightButton,
            "num9Button" => ScientificNineButton,
            "plusButton" => ScientificAddButton,
            "squareRootButton" => ScientificSquareRootButton,
            "backSpaceButton" => ScientificBackspaceButton,
            "multiplyButton" => ScientificMultiplyButton,
            "absButton" => ScientificAbsoluteButton,
            "cubeRootButton" => ScientificCubeRootButton,
            "ceilButton" => ScientificCeilingButton,
            "cosButton" => ScientificCosButton,
            "coshButton" => ScientificCoshButton,
            "cotButton" => ScientificCotButton,
            "cothButton" => ScientificCothButton,
            "cscButton" => ScientificCscButton,
            "cschButton" => ScientificCschButton,
            "degreeButton" => ScientificDegreesButton,
            "dmsButton" => ScientificDmsButton,
            "eulerButton" => ScientificEulerButton,
            "expButton" => ScientificExpButton,
            "factorialButton" => ScientificFactorialButton,
            "floorButton" => ScientificFloorButton,
            "invcosButton" => ScientificInverseCosButton,
            "invcoshButton" => ScientificInverseCoshButton,
            "invcotButton" => ScientificInverseCotButton,
            "invcothButton" => ScientificInverseCothButton,
            "invcscButton" => ScientificInverseCscButton,
            "invcschButton" => ScientificInverseCschButton,
            "invsecButton" => ScientificInverseSecButton,
            "invsechButton" => ScientificInverseSechButton,
            "invsinButton" => ScientificInverseSinButton,
            "invsinhButton" => ScientificInverseSinhButton,
            "invtanButton" => ScientificInverseTanButton,
            "invtanhButton" => ScientificInverseTanhButton,
            "invertButton" => ScientificReciprocalButton,
            "logBase10Button" => ScientificLogButton,
            "logBaseEButton" => ScientificNaturalLogButton,
            "logBaseY" => ScientificLogBaseYButton,
            "openParenthesisButton" => ScientificOpenParenthesisButton,
            "closeParenthesisButton" => ScientificCloseParenthesisButton,
            "piButton" => ScientificPiButton,
            "powerButton" => ScientificPowerButton,
            "powerOf10Button" => ScientificTenPowerButton,
            "powerOfEButton" => ScientificEPowerButton,
            "randButton" => ScientificRandomButton,
            "secButton" => ScientificSecButton,
            "sechButton" => ScientificSechButton,
            "sinButton" => ScientificSinButton,
            "sinhButton" => ScientificSinhButton,
            "tanButton" => ScientificTanButton,
            "tanhButton" => ScientificTanhButton,
            "twoPowerXButton" => ScientificTwoPowerButton,
            "xpower2Button" => ScientificSquareButton,
            "xpower3Button" => ScientificCubeButton,
            "ySquareRootButton" => ScientificRootButton,
            _ => null,
        };

        if (button is null)
        {
            return false;
        }

        button.Classes.Set("keyboardPressed", isPressed);
        return true;
    }
}
