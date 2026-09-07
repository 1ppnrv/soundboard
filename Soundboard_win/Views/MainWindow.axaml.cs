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
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Media;

namespace Soundboard_win.Views;


public partial class MainWindow : Window
{
    private JsonObject SoundsNamesDict = new JsonObject();
    private bool JsonOk = false;

    private Dictionary<string, WasapiPlayer> RunningPlayers = new Dictionary<string, WasapiPlayer>();
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
            string BtnText = filename;
            if (JsonOk)
            {
                if(SoundsNamesDict["names"] is JsonObject names && names[filename] is JsonValue name)
                BtnText = name.GetValue<string>();
            }

            var SoundButtonContent = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = BtnText,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            
            var SoundButton = new Button
            {
                Width = 70,
                Height = 70,
                Content = SoundButtonContent,
                ClickMode = ClickMode.Press,
            };
            SoundButton.Click += PlaySoundFromButton;

            var StopButton = new Button
            {
                Width = 30,
                Height = 20,
                Margin = new Thickness(8),
                Name = BtnText,
                Content = "Stop",
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(0,5,0,0),
                IsVisible = false,
                IsEnabled = false,
                ClickMode = ClickMode.Press
            };
            StopButton.Click += StopSoundFromButton;

            var ManageBtnThreeDots = new TextBlock
            {
                Margin = new Thickness(0,-5,0,0),
                Text = "...",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var ManageBtn = new Button
            {
                Width = 20,
                Height = 14,
                IsVisible = false,
                IsEnabled = false,
                Padding = new Thickness(0,-5,0,0),
                Content = "...",
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(5),
                Background = Brushes.Black
            };
            ManageBtn.Click += ManageSoundFromBtn;


            var ButtonGrid = new Grid
            {
                Margin = new Thickness(5),
                Width = 70,
                Height = 70,
                Name = "BtnGrid" + BtnText
            };
            ButtonGrid.PointerEntered += SoundGridHovered;
            ButtonGrid.PointerExited += SoundGridHovered;


            ButtonGrid.Children.Add(SoundButton);
            ButtonGrid.Children.Add(StopButton);
            ButtonGrid.Children.Add(ManageBtn);

            SoundsPanel.Children.Add(ButtonGrid);
        }
    }

    private void PlaySoundFromButton(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn
        && btn.Content is TextBlock BtnTextBlock
        && BtnTextBlock.Text is string BtnText){

            if (JsonOk
            && SoundsNamesDict["filenames"] is JsonObject filenames
            && filenames[BtnText] is JsonValue filename)
            {
                if(!RunningPlayers.ContainsKey(BtnText)){
                    PlaySound(BtnText, filename.GetValue<string>());
                }
                else
                {
                    PlayPauseSound(BtnText);
                }
            }
            else if (ListFilesLocal(@".\sounds", "*.mp3").Contains(BtnText))
            {
                PlaySound(BtnText, BtnText);
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

        using var DurationReader = new MediaFoundationReader(FilePath);
        var Sound = new MediaFoundationReader(FilePath);

        using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

        using var devices = enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active
        );
        var device = devices[0];

        var TestOutput = new WasapiPlayerBuilder().WithDevice(device).WithLatency(0).Build();

        TestOutput.Init(Sound);

        Button StopButton;
        if (SoundsPanel.Children
            .FirstOrDefault(BtnGrid => BtnGrid.Name == "BtnGrid"+SoundName) is Grid btnGrid
        && btnGrid.Children.ElementAt(1) is Button btn)
        {
            StopButton = btn;
            StopButton.IsEnabled = true;
            StopButton.IsVisible = true;

            TestOutput.PlaybackStopped += (sender, args) => SoundEnded(args, StopButton, SoundName);
        }
        else
        {
            Console.WriteLine("btn not found " + SoundName);
        }
        RunningPlayers.Add(SoundName, TestOutput);
        RunningPlayers[SoundName].Play();
    }

    private void SoundEnded(StoppedEventArgs args, Button button, string sound)
    {
        if (RunningPlayers.ContainsKey(sound)){
            button.IsVisible = false;
            button.IsEnabled = false;
            RunningPlayers[sound].Dispose();
            RunningPlayers.Remove(sound);
        }
    }

    private async void AddSound(object? sender, RoutedEventArgs args)
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
            AddSoundPanel.IsVisible = true;
            AddSoundPanel.IsEnabled = true;

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

                if (FileNames.ContainsKey(SoundName))
                {
                    Console.WriteLine("name already exists!");
                    return;
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
        AddSoundPanel.IsVisible = false;
        AddSoundPanel.IsEnabled = false;
        SaveJson();
        ListSounds();
    }

    private void SoundGridHovered(object? sender, PointerEventArgs args)
    {
        if(sender is Grid Grid
        &&Grid.Children.ElementAt(2) is Button ManageBtn)
        {
            if (ManageBtn.IsVisible)
            {
                ManageBtn.IsVisible = false;
                ManageBtn.IsEnabled = false;
            }
            else if (! ManageSoundPanel.IsVisible)
            {
                ManageBtn.IsVisible = true;
                ManageBtn.IsEnabled = true;
            }
        }
    }

    private void ManageSoundFromBtn(object? sender, RoutedEventArgs args)
    {
        if (sender is Button Btn
        && Btn.Parent is Grid ParentGrid
        && ParentGrid.Children.ElementAt(0) is Button BrotherBtn
        && BrotherBtn.Content is TextBlock BtnTextBlock
        && BtnTextBlock.Text is string BtnText)
        {
            ManageSound(BtnText);
        }
    }

    private void ManageSound(string sound)
    {
        foreach(KeyValuePair<string, WasapiPlayer> Playing in RunningPlayers.AsEnumerable())
        {
            Playing.Value.Stop();
        }
        ManageSoundPanel.IsVisible = true;
        ManageSoundPanel.IsEnabled = true;
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

    private void PlayPauseSoundFromButton(object? sender, RoutedEventArgs args)
    {
        if (sender is Button btn
        && btn.Content is TextBlock BtnTextBlock
        && BtnTextBlock.Text is string BtnText)
        {
            PlayPauseSound(BtnText);
        }
    }
    private void PlayPauseSound(string sound)
    {
        if (RunningPlayers.ContainsKey(sound)){
            if (RunningPlayers[sound].PlaybackState == PlaybackState.Playing){
                RunningPlayers[sound].Pause();
            }else if (RunningPlayers[sound].PlaybackState == PlaybackState.Paused)
            {
                RunningPlayers[sound].Play();
            }
        }
    }

    private void StopSoundFromButton(object? sender, RoutedEventArgs args)
    {
        if (sender is Button btn
        && btn.Parent is Grid ParentGrid
        && ParentGrid.Children.ElementAt(0) is Button BrotherBtn
        && BrotherBtn.Content is TextBlock BtnTextBlock
        && BtnTextBlock.Text is string BtnText)
        {
            StopSound(BtnText);
        }
    }

    private void StopSound(string sound)
    {
        Console.WriteLine("stop");
        if (RunningPlayers.ContainsKey(sound)){
            RunningPlayers[sound].Stop();
        }
    }

    private void SaveJson()
    {
        string JsonString = JsonSerializer.Serialize(SoundsNamesDict);
        File.WriteAllText("sounds/SoundBindings.json", JsonString);
    }

    private void error(string e)
    {
        Console.WriteLine("Error : " + e);
        Close();
    }
}