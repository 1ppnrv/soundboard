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
        string CurDir = System.IO.Directory.GetCurrentDirectory();
        DirectoryInfo SoundsDir = new DirectoryInfo(CurDir + @".\sounds");
        // Console.Write(CurDir + "\n");

        string SoundBindingsFileContent = File.ReadAllText("sounds/SoundBindings.json");
        Console.WriteLine(SoundBindingsFileContent);

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
        
        FileInfo[] Files = SoundsDir.GetFiles("*.mp3");
        string str = "";
        
        foreach(FileInfo file in Files ){
            str = str + "," + file.Name;
            string Content = file.Name;
            if (JsonOk)
            {
                if(SoundsNamesDict["names"] is JsonObject names && names[file.Name] is JsonValue name)
                Content = name.GetValue<string>();
            }

            var button = new Button
            {
                Width = 70,
                Height = 70,
                Content = Content,
                Margin = new Thickness(5)
            };
            button.Click += PlaySound;
            SoundsPanel.Children.Add(button);
        }
    }

    private void PlaySound(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn
            && SoundsNamesDict["filenames"] is JsonObject filenames
            && btn.Content is object BtnContent
            && BtnContent.ToString() is string BtnContentString
            && filenames[BtnContentString] is JsonValue filename){
            
            string FilePath = @".\sounds\" + filename.GetValue<string>();
            Console.WriteLine("Playing \"" + btn.Content + "\" filename : " + filename);

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
    }

    private async void AddSound(object sender, RoutedEventArgs args)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);

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

            if (JsonOk && SoundsNamesDict["names"] is JsonObject Names && SoundsNamesDict["filenames"] is JsonObject FileNames)
            {
                var SoundName = File.Name;

                if (SoundNameTextBox.Text is string SoundNameFromTextBox){
                    SoundName = SoundNameFromTextBox;
                }

                var keyValue = new KeyValuePair<string, JsonNode?>(File.Name, SoundName);
                Names.Add(keyValue);

                var keyValue1 = new KeyValuePair<string, JsonNode?>(SoundName, File.Name);
                FileNames.Add(keyValue1);
            }

            string SoundsPath = Path.Combine(Directory.GetCurrentDirectory(), "sounds");
            string SourcePath = File.Path.LocalPath;
            string DestinationPath = Path.Combine(SoundsPath, Path.GetFileName(SourcePath));
            System.IO.File.Copy(SourcePath, DestinationPath, true);
        }
        AddSoundGrid.IsVisible = false;
        AddSoundGrid.IsEnabled = false;
        SaveJson();
        ListSounds();
    }

    private void SaveJson()
    {
        string JsonString = JsonSerializer.Serialize(SoundsNamesDict);
        File.WriteAllText("sounds/SoundBindings.json", JsonString);
    }

    private void error()
    {
        Console.WriteLine("Error!");
        Close();
    }
}