using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenRecorderLib;

namespace MeetingRecorder {
 public sealed class AudioProcess { public int Id {get;set;} public string Label {get;set;} }
 public sealed class RecordingItem { public string Path {get;set;} public string Label {get;set;} }
 public static class Program {
  [STAThread] public static void Main() {
   try {
    if (Environment.OSVersion.Version.Build < 22000) throw new InvalidOperationException("This prototype requires Windows 11.");
    var app = new Application();
    var controller = new RecorderWindow();
    app.Run(controller.Window);
   } catch (Exception ex) {
    try { File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.log"),DateTime.Now+" "+ex+Environment.NewLine); } catch {}
    MessageBox.Show(ex.ToString(),"Meeting Recorder could not start",MessageBoxButton.OK,MessageBoxImage.Error);
   }
  }
 }
 public sealed class RecorderWindow {
  public Window Window {get;private set;}
  readonly string folder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","Recordings"));
  readonly string logs = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","Logs"));
  readonly Stopwatch clock = new Stopwatch();
  readonly DispatcherTimer timer = new DispatcherTimer();
  Recorder recorder;
  string state="Ready", pendingPath, finalPath, loadedPath;
  IntPtr captureWindow;
  bool closeAfterSave, internalSeek;
  int previewQueued;
  long lastPreview;
  TextBlock status, stateLabel, clockLabel, playbackTime;
  ComboBox windows, processes, microphones;
  CheckBox meetingAudio, micAudio;
  TextBox meetingName;
  StackPanel setup;
  Button start, pause, stop, play, pausePlayback;
  MediaElement player;
  Image preview;
  TextBlock placeholder;
  Slider seek;
  ListBox library;
  T Find<T>(string name) where T:class { return Window.FindName(name) as T; }
  public RecorderWindow() {
   Directory.CreateDirectory(folder); Directory.CreateDirectory(logs);
   using (var input=File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MainWindow.xaml"))) Window=(Window)XamlReader.Load(input);
   status=Find<TextBlock>("Status"); stateLabel=Find<TextBlock>("StateLabel"); clockLabel=Find<TextBlock>("Clock"); playbackTime=Find<TextBlock>("PlaybackTime");
   windows=Find<ComboBox>("WindowPicker"); processes=Find<ComboBox>("ProcessPicker"); microphones=Find<ComboBox>("MicPicker");
   meetingAudio=Find<CheckBox>("MeetingAudio"); micAudio=Find<CheckBox>("MicAudio"); meetingName=Find<TextBox>("MeetingName"); setup=Find<StackPanel>("Setup");
   start=Find<Button>("Record"); pause=Find<Button>("PauseRecord"); stop=Find<Button>("StopRecord"); play=Find<Button>("Play"); pausePlayback=Find<Button>("PausePlayback");
   player=Find<MediaElement>("Player"); preview=Find<Image>("Preview"); placeholder=Find<TextBlock>("Placeholder"); seek=Find<Slider>("Seek"); library=Find<ListBox>("Library");
   Find<TextBlock>("FolderLabel").Text=folder;
   Find<Button>("Refresh").Click+=(s,e)=>RefreshSources();
   Find<Button>("OpenFolder").Click+=(s,e)=>Guard(()=>Process.Start(new ProcessStartInfo("explorer.exe",folder){UseShellExecute=true}));
   start.Click+=(s,e)=>Guard(StartRecording);
   pause.Click+=(s,e)=>Guard(TogglePause);
   stop.Click+=(s,e)=>Guard(StopRecording);
   play.Click+=(s,e)=> { if(!Busy && loadedPath!=null) player.Play(); };
   pausePlayback.Click+=(s,e)=>player.Pause();
   windows.SelectionChanged+=(s,e)=>SelectWindowProcess();
   meetingAudio.Click+=(s,e)=>processes.IsEnabled=meetingAudio.IsChecked==true;
   micAudio.Click+=(s,e)=>microphones.IsEnabled=micAudio.IsChecked==true;
   library.MouseDoubleClick+=(s,e)=> { var item=library.SelectedItem as RecordingItem; if(item!=null && !Busy) LoadRecording(item.Path); };
   player.MediaOpened+=(s,e)=> { seek.Maximum=player.NaturalDuration.HasTimeSpan ? Math.Max(1,player.NaturalDuration.TimeSpan.TotalSeconds):1; seek.IsEnabled=player.NaturalDuration.HasTimeSpan; };
   player.MediaEnded+=(s,e)=> { player.Pause(); player.Position=TimeSpan.Zero; };
   player.MediaFailed+=(s,e)=> { status.Text="Playback failed: "+e.ErrorException.Message+". You can open the MP4 from the recordings folder."; Log(e.ErrorException.ToString()); };
   seek.ValueChanged+=(s,e)=> { if(!internalSeek && !Busy && loadedPath!=null) player.Position=TimeSpan.FromSeconds(seek.Value); };
   Window.Closing+=Closing;
   Window.Closed+=(s,e)=> { timer.Stop(); player.Close(); };
   timer.Interval=TimeSpan.FromMilliseconds(250); timer.Tick+=Tick; timer.Start();
   RefreshSources(); RefreshLibrary();
  }
  bool Busy {get {return recorder!=null;}}
  void Guard(Action action) {try {action();} catch(Exception ex) { Log(ex.ToString()); status.Text=ex.Message; MessageBox.Show(Window,ex.Message,"Meeting Recorder",MessageBoxButton.OK,MessageBoxImage.Warning);}}
  void Log(string text) {try {File.AppendAllText(Path.Combine(logs,"app.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);} catch {}}
  void Post(Action action) {if(!Window.Dispatcher.HasShutdownStarted) Window.Dispatcher.BeginInvoke(action);}
  void RefreshSources() {
   Guard(delegate {
    if(Busy) return;
    var old=windows.SelectedItem as RecordableWindow;
    var list=Recorder.GetWindows().Where(w=>w.Pid.GetValueOrDefault()!=Process.GetCurrentProcess().Id).OrderBy(w=>w.Title).ToList();
    windows.ItemsSource=list;
    if(old!=null) windows.SelectedItem=list.FirstOrDefault(w=>w.Handle==old.Handle);
    var all=new List<AudioProcess>();
    foreach(var proc in Process.GetProcesses()) {using(proc) { try {all.Add(new AudioProcess{Id=proc.Id,Label=proc.ProcessName+" (PID "+proc.Id+")"});} catch {}}}
    processes.ItemsSource=all.OrderBy(p=>p.Label).ToList();
    var mics=Recorder.GetSystemAudioCaptureDevices();
    microphones.ItemsSource=mics;
    microphones.SelectedItem=mics.FirstOrDefault(m=>m.IsDefaultDevice) ?? mics.FirstOrDefault();
    SelectWindowProcess();
    if(list.Count==0) status.Text="Open your meeting window, then click Refresh.";
   });
  }
  void SelectWindowProcess() {
   var win=windows.SelectedItem as RecordableWindow;
   if(win==null || processes.ItemsSource==null) return;
   processes.SelectedItem=((IEnumerable<AudioProcess>)processes.ItemsSource).FirstOrDefault(p=>p.Id==win.Pid.GetValueOrDefault());
   status.Text="Selected: "+win.Title+". Check the audio app and microphone before recording.";
  }
  string SafeName(string name) {
   var bad=Path.GetInvalidFileNameChars();
   string clean=new string(name.Trim().Select(c=>bad.Contains(c)?'_':c).ToArray()).Trim('.',' ');
   if(clean.Length==0) clean="Meeting";
   return clean.Substring(0,Math.Min(70,clean.Length));
  }
  void StartRecording() {
   if(Busy) return;
   var win=windows.SelectedItem as RecordableWindow;
   if(win==null || !Native.IsWindow(win.Handle)) throw new InvalidOperationException("Choose an open meeting window first.");
   if(Native.IsIconic(win.Handle)) throw new InvalidOperationException("Restore the meeting window before starting.");
   var audio=new List<AudioSourceBase>();
   if(meetingAudio.IsChecked==true) {
    var proc=processes.SelectedItem as AudioProcess;
    if(proc==null) throw new InvalidOperationException("Choose the meeting audio app.");
    using(var alive=Process.GetProcessById(proc.Id)) { if(alive.HasExited) throw new InvalidOperationException("The audio app has closed. Refresh and select it again."); }
    audio.Add(new ProcessAudioSource(proc.Id));
   }
   if(micAudio.IsChecked==true) {
    var mic=microphones.SelectedItem as RecordableAudioCaptureDevice;
    if(mic==null) throw new InvalidOperationException("No microphone selected. Connect one and refresh, or uncheck microphone recording.");
    audio.Add(new CaptureAudioSource(mic.DeviceName));
   }
   // Never fall back to system-wide audio: only the explicitly selected process tree is captured.
   var options=RecorderOptions.Default;
   options.SourceOptions.RecordingSources=new List<RecordingSourceBase>{new WindowRecordingSource(win.Handle){IsBorderRequired=true}};
   options.AudioOptions.IsAudioEnabled=audio.Count>0;
   options.AudioOptions.AudioSources=audio;
   options.VideoEncoderOptions.Framerate=30;
   options.OutputOptions.OutputFrameSize=new ScreenSize(1920,1080);
   options.VideoEncoderOptions.IsHardwareEncodingEnabled=false;
   options.OutputOptions.IsVideoFramePreviewEnabled=true;
   options.OutputOptions.VideoFramePreviewSize=new ScreenSize(640,0);
   options.LogOptions.IsLogEnabled=true;
   options.LogOptions.LogFilePath=Path.Combine(logs,"capture-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");
   finalPath=Path.Combine(folder,SafeName(meetingName.Text)+" - "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".mp4");
   pendingPath=finalPath.Substring(0,finalPath.Length-4)+".recording.mp4";
   player.Stop(); player.Close(); loadedPath=null; player.Visibility=Visibility.Collapsed;
   placeholder.Visibility=Visibility.Visible; placeholder.Text="Starting meeting capture..."; preview.Source=null; preview.Visibility=Visibility.Visible;
   clock.Reset(); captureWindow=win.Handle;
   recorder=Recorder.CreateRecorder(options);
   recorder.OnStatusChanged+=OnStatus;
   recorder.OnRecordingComplete+=OnComplete;
   recorder.OnRecordingFailed+=OnFailed;
   recorder.OnFrameRecorded+=OnFrame;
   SetState("Starting"); status.Text="Starting window video and WASAPI audio capture...";
   Log("Start "+win.Title+" window PID="+win.Pid+" audio="+(meetingAudio.IsChecked==true ? ((AudioProcess)processes.SelectedItem).Label : "off")+" mic="+(micAudio.IsChecked==true));
   try {recorder.Record(pendingPath);} catch {ReleaseRecorder(); SetState("Ready"); throw;}
  }
  void SetState(string value) {
   state=value; stateLabel.Text=value.ToUpperInvariant();
   stateLabel.Foreground=new SolidColorBrush(value=="Recording" ? Color.FromRgb(190,55,65):Color.FromRgb(23,108,97));
   setup.IsEnabled=!Busy; start.IsEnabled=!Busy; library.IsEnabled=!Busy;
   pause.IsEnabled=Busy && (value=="Recording" || value=="Paused");
   stop.IsEnabled=pause.IsEnabled;
   pause.Content=value=="Paused" ? "Resume recording":"Pause recording";
   play.IsEnabled=!Busy && loadedPath!=null; pausePlayback.IsEnabled=play.IsEnabled;
   if(Busy) seek.IsEnabled=false;
  }
  void OnStatus(object sender,RecordingStatusEventArgs e) {
   var value=e.Status;
   Post(delegate {
    if(sender!=recorder) return;
    if(value==RecorderStatus.Recording && state!="Saving") {clock.Start();SetState("Recording");status.Text="Recording the meeting window, selected app audio, and enabled microphone.";}
    else if(value==RecorderStatus.Paused && state!="Saving") {clock.Stop();SetState("Paused");status.Text="Recording paused. Resume to continue.";}
    else if(value==RecorderStatus.Finishing) {clock.Stop();SetState("Saving");status.Text="Finalizing your MP4. Keep the app open until saving finishes.";}
   });
  }
  void TogglePause() {if(recorder==null) return; pause.IsEnabled=false; if(state=="Paused") recorder.Resume(); else if(state=="Recording") recorder.Pause();}
  void StopRecording() {if(recorder==null || state=="Saving") return; clock.Stop();SetState("Saving");status.Text="Finalizing your MP4...";recorder.Stop();}
  void OnComplete(object sender,RecordingCompleteEventArgs e) {
   string path=e.FilePath;
   Post(delegate {
    if(sender!=recorder) return;
    clock.Stop(); ReleaseRecorder(); SetState("Ready");
    try {
     if(!File.Exists(path) || new FileInfo(path).Length==0) throw new IOException("The recording file is empty.");
     File.Move(path,finalPath); Log("Saved "+finalPath);
     RefreshLibrary(); LoadRecording(finalPath); status.Text="Saved. Your meeting video is ready to play.";
     if(closeAfterSave) Window.Close();
    } catch(Exception ex) {closeAfterSave=false;Log(ex.ToString());status.Text="Could not finish saving: "+ex.Message+". Check the recordings folder for the .recording.mp4 file.";}
   });
  }
  void OnFailed(object sender,RecordingFailedEventArgs e) {
   string error=e.Error;
   Post(delegate {if(sender!=recorder) return; clock.Stop();ReleaseRecorder();closeAfterSave=false;SetState("Ready");preview.Visibility=Visibility.Collapsed;placeholder.Visibility=Visibility.Visible;placeholder.Text="Recording stopped";status.Text="Recording failed: "+error;Log(error);MessageBox.Show(Window,error+"\n\nSee the Logs folder for details. Any partial recording is preserved.","Recording failed",MessageBoxButton.OK,MessageBoxImage.Error);});
  }
  void OnFrame(object sender,FrameRecordedEventArgs e) {
   // The native buffer only lives during this callback. Copy before posting to WPF.
   if(e.BitmapData==null || e.BitmapData.Data==IntPtr.Zero || e.BitmapData.Width<=0 || e.BitmapData.Height<=0) return;
   long now=Stopwatch.GetTimestamp();
   if(now-System.Threading.Interlocked.Read(ref lastPreview)<Stopwatch.Frequency/5 || System.Threading.Interlocked.CompareExchange(ref previewQueued,1,0)!=0) return;
   System.Threading.Interlocked.Exchange(ref lastPreview,now);
   try {
    var data=e.BitmapData;
    var bitmap=BitmapSource.Create(data.Width,data.Height,96,96,PixelFormats.Bgra32,null,data.Data,data.Length,data.Stride);bitmap.Freeze();
    Post(delegate {try {if(sender==recorder) {preview.Source=bitmap;placeholder.Visibility=Visibility.Collapsed;}} finally {System.Threading.Interlocked.Exchange(ref previewQueued,0);}});
   } catch {System.Threading.Interlocked.Exchange(ref previewQueued,0);}
  }
  void ReleaseRecorder() {
   var previous=recorder;recorder=null;
   if(previous!=null) {previous.OnStatusChanged-=OnStatus;previous.OnRecordingComplete-=OnComplete;previous.OnRecordingFailed-=OnFailed;previous.OnFrameRecorded-=OnFrame;previous.Dispose();}
   preview.Visibility=Visibility.Collapsed;
  }
  void RefreshLibrary() {
   library.ItemsSource=new DirectoryInfo(folder).GetFiles("*.mp4").Where(f=>!f.Name.EndsWith(".recording.mp4",StringComparison.OrdinalIgnoreCase)).OrderByDescending(f=>f.LastWriteTime).Select(f=>new RecordingItem{Path=f.FullName,Label=Path.GetFileNameWithoutExtension(f.Name)+"  |  "+(f.Length/1048576.0).ToString("0.0")+" MB"}).ToList();
  }
  void LoadRecording(string path) {
   if(Busy) return;
   Guard(delegate {player.Stop();player.Close();loadedPath=path;player.Source=new Uri(path);player.Visibility=Visibility.Visible;preview.Visibility=Visibility.Collapsed;placeholder.Visibility=Visibility.Collapsed;player.Play();SetState("Ready");status.Text="Playing: "+Path.GetFileName(path);});
  }
  void Tick(object sender,EventArgs e) {
   clockLabel.Text=clock.Elapsed.ToString(@"hh\:mm\:ss");
   if(Busy && (state=="Recording" || state=="Paused")) {
    if(!Native.IsWindow(captureWindow) || Native.IsIconic(captureWindow)) {Guard(StopRecording);status.Text="The meeting window closed or was minimized. Saving the recording.";}
   }
   if(!Busy && loadedPath!=null && player.NaturalDuration.HasTimeSpan) {
    internalSeek=true;seek.Value=player.Position.TotalSeconds;internalSeek=false;
    playbackTime.Text=player.Position.ToString(@"hh\:mm\:ss")+" / "+player.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
   }
  }
  void Closing(object sender,CancelEventArgs e) {
   if(!Busy) return;
   e.Cancel=true;
   if(state=="Saving") {status.Text="Please wait for the MP4 to finish saving.";return;}
   if(state=="Starting") {status.Text="Capture is starting. Wait for recording to begin, then stop and save.";return;}
   if(MessageBox.Show(Window,"Stop recording, save the MP4, and close?","Recording in progress",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes) {closeAfterSave=true;Guard(StopRecording);}
  }
 }
 internal static class Native {
  [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(IntPtr hwnd);
 }
}
