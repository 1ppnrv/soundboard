using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;

namespace Soundboard_win.Views;


public partial class MainWindow : Window
{
    private MMDevice? OutputDevice;
    private JsonObject SoundsNamesDict = new JsonObject();
    private bool JsonOk = false;

    private Dictionary<string, WasapiPlayer> RunningPlayers = new Dictionary<string, WasapiPlayer>();
    public MainWindow()
    {
        InitializeComponent();

        using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active
        );
        OutputDevice = devices[0];

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

        foreach (string filename in Files)
        {
            str = str + "," + filename;
            string BtnText = filename;
            if (JsonOk)
            {
                if (SoundsNamesDict["names"] is JsonObject names && names[filename] is JsonValue name)
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
                Padding = new Thickness(0, 0, 0, 0),
                IsVisible = false,
                IsEnabled = false,
                ClickMode = ClickMode.Press,
                Background = Brushes.Black
            };
            StopButton.Click += StopSoundFromButton;

            var ManageBtnThreeDots = new TextBlock
            {
                Margin = new Thickness(5, -8, 0, 0),
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
                Padding = new Thickness(0, -5, 0, 0),
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
        && BtnTextBlock.Text is string BtnText)
        {

            if (JsonOk
            && SoundsNamesDict["filenames"] is JsonObject filenames
            && filenames[BtnText] is JsonValue filename)
            {
                if (!RunningPlayers.ContainsKey(BtnText))
                {
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

        var Sound = new MediaFoundationReader(FilePath);

        var Output = new WasapiPlayerBuilder().WithDevice(OutputDevice).WithLatency(50).Build();

        Output.Init(Sound);

        Button StopButton;
        if (SoundsPanel.Children
            .FirstOrDefault(BtnGrid => BtnGrid.Name == "BtnGrid" + SoundName) is Grid btnGrid
        && btnGrid.Children.ElementAt(1) is Button btn)
        {
            StopButton = btn;
            StopButton.IsEnabled = true;
            StopButton.IsVisible = true;

            Output.PlaybackStopped += (sender, args) => SoundEnded(args, StopButton, SoundName);
        }
        else
        {
            Console.WriteLine("btn not found " + SoundName);
        }
        RunningPlayers.Add(SoundName, Output);
        RunningPlayers[SoundName].Play();
    }

    private void SoundEnded(StoppedEventArgs args, Button button, string sound)
    {
        if (RunningPlayers.ContainsKey(sound))
        {
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
            AllowMultiple = true,
            FileTypeFilter = fileTypes
        });

        foreach (IStorageFile File in files)
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
            ;
            string filename = File.Name;

            while (ListFilesLocal(@".\sounds", "*.mp3").Contains(File.Name))
            {
                filename = "1_" + filename;
            }

            // add sound to sounds dir and to json

            if (JsonOk && SoundsNamesDict["names"] is JsonObject Names && SoundsNamesDict["filenames"] is JsonObject FileNames)
            {
                var SoundName = File.Name;

                if (SoundNameTextBox.Text is string SoundNameFromTextBox && SoundNameFromTextBox != "")
                {
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
        if (sender is Grid Grid
        && Grid.Children.ElementAt(2) is Button ManageBtn)
        {
            if (ManageBtn.IsVisible)
            {
                ManageBtn.IsVisible = false;
                ManageBtn.IsEnabled = false;
            }
            else if (!ManageSoundPanel.IsVisible)
            {
                ManageBtn.IsVisible = true;
                ManageBtn.IsEnabled = true;
            }
        }
    }

    private async void ManageSoundFromBtn(object? sender, RoutedEventArgs args)
    {
        if (sender is Button Btn
        && Btn.Parent is Grid ParentGrid
        && ParentGrid.Children.ElementAt(0) is Button BrotherBtn
        && BrotherBtn.Content is TextBlock BtnTextBlock
        && BtnTextBlock.Text is string BtnText)
        {
            ManageSound(BtnText, BtnTextBlock);
        }
    }

    private async void ManageSound(string sound, TextBlock Tochange)
    {
        if (RunningPlayers.ContainsKey(sound))
        {
            RunningPlayers[sound].Stop();
        }
        ManageSoundPanel.IsVisible = true;
        ManageSoundPanel.IsEnabled = true;
        ManageSoundNameTextBox.Text = sound;
        string FileName = "";

        if (JsonOk
        && SoundsNamesDict["filenames"] is JsonObject filenames
        && filenames[sound] is JsonValue filename
        && filename.ToString() is string filenameString)
        {
            FileName = filenameString;
            SoundFileNameTextBlock.Text = filenameString;
        }
        else if (ListFilesLocal(@".\sounds", "*.mp3").Contains(sound))
        {
            FileName = sound;
            SoundFileNameTextBlock.Text = sound;
        }
        else
        {
            error("file not found" + sound);
        }

        bool Delete = false;
        bool Cancel = false;
        bool Validate = false;

        while (!(Delete || Cancel || Validate))
        {
            Delete = DeleteSound.IsPressed;
            Cancel = CancelManageSound.IsPressed;
            Validate = ValidateManageSound.IsPressed;
            await Task.Delay(10);
        }

        if (Cancel)
        {
            ManageSoundPanel.IsVisible = false;
            ManageSoundPanel.IsEnabled = false;
            return;
        }
        if (Validate)
        {
            if (ManageSoundNameTextBox.Text != sound
            && Tochange.Parent is Button parent
            && parent.Parent is Grid parentGrid)
            {
                ModifySoundNameJson(sound, ManageSoundNameTextBox.Text);
                sound = ManageSoundNameTextBox.Text;
            }
            if (SoundFileNameTextBlock.Text != FileName
            && SoundFileNameTextBlock.Text is string)
            {
                string SoundsPath = Path.Combine(Directory.GetCurrentDirectory(), "sounds");
                string OldFile = Path.Combine(SoundsPath, FileName);
                string NewFileName = SoundFileNameTextBlock.Text.Substring(SoundFileNameTextBlock.Text.LastIndexOf('\\')).Remove(0,1);
                string DestFile = Path.Combine(SoundsPath, NewFileName);
                Console.WriteLine(DestFile);

                File.Delete(OldFile);

                File.Copy(SoundFileNameTextBlock.Text, DestFile, true);
                if (JsonOk
                && SoundsNamesDict["filenames"] is JsonObject filenames1
                && filenames1[sound] is JsonValue filename1
                && filename1.ToString() is string filenameString1)
                {
                    if (SoundsNamesDict["names"] is JsonObject names
                    && names[filenameString1] is JsonValue name
                    && name.ToString() is string nameString)
                    {
                        names.Remove(filenameString1);
                        var keyValue = new KeyValuePair<string, JsonNode?>(NewFileName, sound);
                        names.Add(keyValue);

                        filenames1.Remove(nameString);
                        var keyValue1 = new KeyValuePair<string, JsonNode?>(sound, NewFileName);
                        filenames1.Add(keyValue1);
                        SaveJson();
                    }
                }
            }
        }
        else if (Delete)
        {
            string SoundsPath = Path.Combine(Directory.GetCurrentDirectory(), "sounds");
            string OldFile = Path.Combine(SoundsPath, FileName);

            File.Delete(OldFile);

            if (JsonOk
            && SoundsNamesDict["filenames"] is JsonObject filenames1
            && filenames1[sound] is JsonValue filename1
            && filename1.ToString() is string filenameString1)
            {
                if (SoundsNamesDict["names"] is JsonObject names
                && names[filenameString1] is JsonValue name
                && name.ToString() is string nameString)
                {
                    names.Remove(filenameString1);
                    filenames1.Remove(nameString);

                    SaveJson();
                }
            }
            int count = SoundsPanel.Children.Count;
            for (int i = 0; i < count; i++)
            {
                if (SoundsPanel.Children[i].Name == "BtnGrid" + sound)
                {
                    SoundsPanel.Children.RemoveAt(i);
                    break;
                }
            }
        }
        ManageSoundPanel.IsVisible = false;
        ManageSoundPanel.IsEnabled = false;
        ListSounds();
    }

    private async void SelectFile(object? sender, RoutedEventArgs args)
    {
        var topLevel = GetTopLevel(this);

        if (!(topLevel is TopLevel))
        {
            return;
        }

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

        IStorageFile File = files.First();

        SoundFileNameTextBlock.Text = File.Path.LocalPath;
    }

    private void ModifySoundNameJson(string oldName, string newName)
    {
        if (JsonOk
        && SoundsNamesDict["filenames"] is JsonObject filenames
        && filenames[oldName] is JsonValue filename
        && filename.ToString() is string filenameString)
        {
            if (SoundsNamesDict["names"] is JsonObject names
            && names[filenameString] is JsonValue name
            && name.ToString() is string nameString)
            {
                names.Remove(filenameString);
                names.Insert(0, filenameString, newName);

                filenames.Remove(nameString);
                filenames.Insert(0, newName, filenameString);
                SaveJson();
            }
        }
    }

    private List<string> ListFilesLocal(string dir, string pattern)
    {
        string AbsolutePath = Path.Combine(Directory.GetCurrentDirectory(), dir);
        DirectoryInfo Dir = new DirectoryInfo(AbsolutePath);
        FileInfo[] Files = Dir.GetFiles(pattern);
        List<string> filenames = [];
        foreach (FileInfo file in Files)
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
        if (RunningPlayers.ContainsKey(sound))
        {
            if (RunningPlayers[sound].PlaybackState == PlaybackState.Playing)
            {
                RunningPlayers[sound].Pause();
            }
            else if (RunningPlayers[sound].PlaybackState == PlaybackState.Paused)
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
        if (RunningPlayers.ContainsKey(sound))
        {
            RunningPlayers[sound].Stop();
        }
    }

    private void SaveJson()
    {
        string JsonString = JsonSerializer.Serialize(SoundsNamesDict, SoundboardJsonContext.Default.JsonObject);
        File.WriteAllText("sounds/SoundBindings.json", JsonString);
    }

    private void error(string e)
    {
        Console.WriteLine("Error : " + e);
        Close();
    }
    protected override void OnClosed(EventArgs e)
{
    foreach (var player in RunningPlayers.Values)
        player.Dispose();

    OutputDevice?.Dispose();

    base.OnClosed(e);
}
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonObject))]
internal partial class SoundboardJsonContext : JsonSerializerContext
{
}