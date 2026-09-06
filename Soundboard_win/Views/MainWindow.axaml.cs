using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Text.Json.Nodes;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Threading.Tasks;
using NAudio.SoundFile;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Layout;

namespace Soundboard_win.Views;


public partial class MainWindow : Window
{
    private JsonObject SoundsNamesDict = new JsonObject();
    private bool JsonOk = false;
    public MainWindow()
    {
        InitializeComponent();
        ListSounds();
    }

    private void ListSounds()
    {
        SoundsPanel.Children.Clear();
        
        // Console.Write(CurDir + "\n");

        string SoundBindingsFileContent = File.ReadAllText("sounds/SoundBindings.json");

        if (SoundBindingsFileContent != null)
        {
            SoundsNamesDict = JsonNode.Parse(SoundBindingsFileContent)?.AsObject() ?? new JsonObject();
            if (SoundsNamesDict["names"] is JsonObject _ && SoundsNamesDict["filenames"] is JsonObject _)
            {
                JsonOk = true;
            }

        }
        else
        {
            Console.WriteLine("json file not found or not valid");
        }

        List<string> Files = ListFilesLocal("sounds", "*.mp3");
        string str = "";
        
        foreach(string filename in Files){
            str = str + "," + filename;
            string Content = filename;
            if (JsonOk)
            {
                if(SoundsNamesDict["names"] is JsonObject names && names[filename] is JsonValue name)
                Content = name.GetValue<string>();
            }
            
            var SoundButton = new Button
            {
                Width = 70,
                Height = 70,
                Content = Content
            };

            var PauseButton = new Button
            {
                Width = 25,
                Height = 25,
                Margin = new Thickness(8),
                Content = "P",
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(2),
                IsVisible = false,
                IsEnabled = false
            };

            var StopButton = new Button
            {
                Width = 25,
                Height = 25,
                Margin = new Thickness(8),
                Content = "S",
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(2),
                IsVisible = false,
                IsEnabled = false
            };

            var ButtonGrid = new Grid
            {
                Margin = new Thickness(5),
                Width = 70,
                Height = 70
            };
            SoundButton.Click += PlaySoundFromButton;
            ButtonGrid.Children.Add(SoundButton);
            ButtonGrid.Children.Add(PauseButton);
            ButtonGrid.Children.Add(StopButton);

            SoundsPanel.Children.Add(ButtonGrid);
        }
    }

    private void PlaySoundFromButton(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn
        && btn.Content is object BtnContent
        && BtnContent.ToString() is string BtnContentString){
            if (JsonOk
            && SoundsNamesDict["filenames"] is JsonObject filenames
            && filenames[BtnContentString] is JsonValue filename)
            {
                PlaySound(BtnContentString, filename.GetValue<string>());
            }
            else if (ListFilesLocal(@".\sounds", "*.mp3").Contains(BtnContentString))
            {
                PlaySound(BtnContentString, BtnContentString);
            }
            else
            {
                error("BtnContent does not match any known sound!");
            }
        }
    }

    private void PlaySound(string SoundName, string filename)
    {
        string FilePath = @".\sounds\" + filename;
        Console.WriteLine("Playing \"" + SoundName + "\" filename : " + filename);

        if (OperatingSystem.IsWindows())
        {
            using var DurationReader = new MediaFoundationReader(FilePath);
            using var Sound = new MediaFoundationReader(FilePath);

            using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

            using var devices = enumerator.EnumerateAudioEndPoints(
                DataFlow.Render,
                DeviceState.Active
            );
            using var device = devices[0];

            var Output = new WasapiOut(
                device,
                AudioClientShareMode.Shared,
                false,
                0
            );

            Output.Init(Sound);
            Output.Play();

            TimeSpan Duration = DurationReader.TotalTime;
            Task.Delay(Duration);
        }
    }

    private async void AddSound(object sender, RoutedEventArgs args)
    {
        var toplevel = GetTopLevel(this);

        if (!(toplevel is TopLevel topLevel))
        {
            return;
        }

        // Start async operation to open the dialog.
        IReadOnlyList<FilePickerFileType> fileTypes = new[]
            {
                new FilePickerFileType("MP3 Files")
                {
                    Patterns = new[] { "*.mp3" }
                }
            };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open MP3 File",
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        foreach(var File in files)
        {
            SoundNameTextBox.Clear();
            SoundNameTextBlock.Text = File.Name;
            AddSoundGrid.IsVisible = true;
            AddSoundGrid.IsEnabled = true;

            while (!ValidateAddSound.IsPressed)
            {
                await Task.Delay(50);
            }

            // check if a file with same name exists and rename the copy if yes
            List<string> filenames = ListFilesLocal(@".\sounds", "*.mp3");
            string filename;

            if (! filenames.Contains(File.Name))
            {
                filename = File.Name;
            }
            else
            {
                filename = "1_" + File.Name;
            }

            // add sound to sounds dir and to json

            if (JsonOk && SoundsNamesDict["names"] is JsonObject Names && SoundsNamesDict["filenames"] is JsonObject FileNames)
            {
                var SoundName = File.Name;

                if (SoundNameTextBox.Text is string SoundNameFromTextBox && SoundNameFromTextBox != ""){
                    SoundName = SoundNameFromTextBox;
                }
                
                string SoundsPath = Path.Combine(Directory.GetCurrentDirectory(), "sounds");
                string SourcePath = File.Path.LocalPath;
                
                string DestinationPath = Path.Combine(SoundsPath, filename);
                System.IO.File.Copy(SourcePath, DestinationPath, true);

                var keyValue = new KeyValuePair<string, JsonNode?>(filename, SoundName);
                Names.Add(keyValue);

                var keyValue1 = new KeyValuePair<string, JsonNode?>(SoundName, filename);
                FileNames.Add(keyValue1);
            }
            else
            {
                //if no json just copy
                string SoundsPath = Path.Combine(Directory.GetCurrentDirectory(), "sounds");
                string SourcePath = File.Path.LocalPath;
                string DestinationPath = Path.Combine(SoundsPath, filename);
                System.IO.File.Copy(SourcePath, DestinationPath, true);
            }
            
        }
        AddSoundGrid.IsVisible = false;
        AddSoundGrid.IsEnabled = false;
        SaveJson();
        ListSounds();
    }

    private List<string> ListFilesLocal(string dir, string pattern)
    {
        string AbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), dir);
        DirectoryInfo Dir = new DirectoryInfo(AbsolutePath);
        FileInfo[] Files = Dir.GetFiles(pattern);
        List<string> filenames = [];
        foreach(FileInfo file in Files)
        {
            filenames.Add(file.Name);
        }
        return filenames;
    }

    private void SaveJson()
    {
        string JsonString = JsonSerializer.Serialize(SoundsNamesDict);
        File.WriteAllText("sounds/SoundBindings.json", JsonString);
    }

    private void error(string e)
    {
        Console.WriteLine("Error!" + e);
        Close();
    }
}