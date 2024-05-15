using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WMPLib;
using static System.Net.Mime.MediaTypeNames;
using WpfAnimatedGif;
using Color = System.Windows.Media.Color;

namespace globulator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    CommandConsole console;
    private int fileIndex = 0;

    readonly WindowsMediaPlayer notifySFX = new()
    {
        URL = "sfx/notify.mp3"
    };
    readonly WindowsMediaPlayer successSFX = new()
    {
        URL = "sfx/success.mp3"
    };
    readonly WindowsMediaPlayer errorSFX = new()
    {
        URL = "sfx/error.mp3"
    };

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;

        console = new();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        successSFX.controls.stop();
        errorSFX.controls.stop();

        AppendTextToConsole(($"{DateTime.Now:HH:mm:ss}; ", Color.FromRgb(213, 63, 119)));
        AppendTextToConsole(($"{console.CurrentDirectory}; ", Color.FromRgb(255, 239, 0)));
        AppendTextToConsole(($"{console.CurrentGlobName}; ", Color.FromRgb(255, 164, 0)));
        AppendTextToConsole((">> Program booted..\r", Color.FromRgb(0, 155, 119)));

        viewedFilePath.Content = console.CurrentDirectory;

        if (console.isMuted)
            notifySFX.controls.stop();
    }

    private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        sendButton.IsDefault = true;
    }

    private void sendButton_Click(object sender, RoutedEventArgs e)
    {
        TextRange viewingBoxText = new(
            viewingBox.Document.ContentStart,
            viewingBox.Document.ContentEnd
        );

        AppendTextToConsole(($"{DateTime.Now:HH:mm:ss}; ", Color.FromRgb(213, 63, 119)));
        AppendTextToConsole(($"{console.CurrentDirectory}; ", Color.FromRgb(255, 239, 0)));
        AppendTextToConsole(($"{console.CurrentGlobName}; ", Color.FromRgb(255, 164, 0)));
        AppendTextToConsole(($"<< {commandBox.Text}", Color.FromRgb(0, 188, 227)));
        (string, Color) response = console.ProcessCommand(commandBox.Text, (viewingBoxText.Text, viewedFilePath.Content.ToString()));
        commandBox.Clear();

        string responseText = response.Item1;
        Color responseColour = response.Item2;

        if (!console.isMuted)
        {
            if (responseColour == Color.FromRgb(205, 0, 26))
                errorSFX.controls.play();
            else if (responseColour == Color.FromRgb(0, 155, 119))
                successSFX.controls.play();
            else
                notifySFX.controls.play();
        }

        AppendTextToConsole(($"\r{DateTime.Now:HH:mm:ss}; ", Color.FromRgb(213, 63, 119)));
        AppendTextToConsole(($"{console.CurrentDirectory}; ", Color.FromRgb(255, 239, 0)));
        AppendTextToConsole(($"{console.CurrentGlobName}; ", Color.FromRgb(255, 164, 0)));
        AppendTextToConsole(($">> {responseText}\r", responseColour));
        consoleOutput.ScrollToEnd();

        sendButton.IsDefault = false;
        console.AllFilesInCurrentPath = Directory.GetFiles(console.CurrentDirectory);
    }

    public void AppendTextToConsole((string, Color) text)
    {
        TextRange tr = new(consoleOutput.Document.ContentEnd, consoleOutput.Document.ContentEnd)
        {
            Text = text.Item1
        };

        try
        {
            tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                new SolidColorBrush(text.Item2));
        }
        catch (FormatException) { }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        window.KeyDown += HandleKeyPress;
    }

    private void HandleKeyPress(object sender, KeyEventArgs e)
    {
        // need to check if it's these calls at all, otherwise it will set the default accept button to null on any key
        // this will prevent a user from pressing return to enter a command
        if (e.Key == Key.F11 || e.Key == Key.F12)
        {
            if (e.Key == Key.F11 && fileIndex != 0)
                fileIndex--;

            else if (e.Key == Key.F12 && fileIndex < console.AllFilesInCurrentPath.Length - 1)
                fileIndex++;

            try
            {
                string path = console.AllFilesInCurrentPath[fileIndex];
                viewedFilePath.Content = path;
                if (path.EndsWith("png") || path.EndsWith("jpg") || path.EndsWith("jpeg") || path.EndsWith("bmp") || path.EndsWith("gif"))
                {
                    pictureBox.Visibility = Visibility.Visible;
                    viewingBox.Visibility = Visibility.Hidden;

                    BitmapImage im = new();
                    im.BeginInit();
                    im.UriSource = new Uri(path);
                    im.EndInit();

                    ImageBehavior.SetAnimatedSource(pictureBox, im);
                }
                    
                else
                {
                    viewingBox.Visibility = Visibility.Visible;
                    pictureBox.Visibility = Visibility.Hidden;

                    FlowDocument textDoc = new();

                    Run textRun = new(File.ReadAllText(path));

                    Paragraph text = new();
                    text.Inlines.Add(textRun);

                    textDoc.Blocks.Add(text);

                    viewingBox.Document = textDoc;
                }
                    
            }
            catch (IndexOutOfRangeException)
            {
                viewingBox.Visibility = Visibility.Visible;
                pictureBox.Visibility = Visibility.Hidden;

                FlowDocument textDoc = new();

                Run textRun = new("No files in selected directory.");

                Paragraph text = new();
                text.Inlines.Add(textRun);

                textDoc.Blocks.Add(text);

                viewingBox.Document = textDoc;
            }
        }
    }

    private void viewingBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        sendButton.IsDefault = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Begin dragging the window
        this.DragMove();
    }

    private void consoleOutput_TextChanged(object sender, TextChangedEventArgs e)
    {

    }
}