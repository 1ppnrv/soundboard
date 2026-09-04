using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Text.Json.Nodes;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Threading.Tasks;
using NAudio.SoundFile;

namespace Soundboard_win.Views;


public partial class MainWindow : Window
{
    private JsonObject SoundsNamesDict = new JsonObject();
    private bool JsonOk = false;
    public MainWindow()
    {
        InitializeComponent();

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
                Content = Content

            };
            button.Click += PlaySound;
            MainPanel.Children.Add(button);
        }
        // Console.WriteLine(str);
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
                var DurationReader = new MediaFoundationReader(FilePath);
                var Sound = new MediaFoundationReader(FilePath);

                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

                var devices = enumerator.EnumerateAudioEndPoints(
                    DataFlow.Render,
                    DeviceState.Active
                );
                var device = devices[0];

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
}