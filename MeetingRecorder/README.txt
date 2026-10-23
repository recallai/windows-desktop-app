MEETING RECORDER - WINDOWS PROTOTYPE

START
1. Extract the entire ZIP to a writable folder on your Windows 11 x64 PC.
2. Double-click Start.cmd. It compiles the app and opens the recorder.
   Visual Studio and Node.js are not required. The launcher uses Windows'
   built-in .NET Framework C# compiler. There are no runtime downloads.
3. Open the meeting and click Refresh windows and devices.
4. Choose the MEETING WINDOW, then verify the audio app matches your meeting.
   The window's process is selected automatically; change it if your meeting
   uses another audio process. Choose the microphone you use in the meeting.
5. Click Start recording. Keep the meeting window open and unminimized.
6. Click Stop and save. Wait until saving completes. The MP4 plays in the app.
   Previously saved videos appear in the list; double-click one to play it.

REQUIREMENTS
- Windows 11 x64, with .NET Framework 4.8 and Media Foundation enabled.
- Microsoft Visual C++ x64 Redistributable. If the launcher reports it missing:
  https://aka.ms/vs/17/release/vc_redist.x64.exe
- Microphone access enabled in Windows Settings > Privacy & security > Microphone,
  including permission for desktop apps.
- On Windows N, enable the Media Feature Pack through Optional Features.

WHAT IT DOES
- Windows Graphics Capture records only your selected window.
- WASAPI process loopback records the selected audio app and its child processes.
- WASAPI input capture records the selected microphone.
- ScreenRecorderLib mixes audio and synchronizes it with window frames, then
  encodes through Windows Media Foundation into H.264/AAC MP4.
- Output is 1920x1080 at up to 30 fps. Window aspect ratio is preserved with
  letterboxing. Software video encoding is enabled for hardware compatibility.
- A live video preview, recording timer, pause/resume, and playback seek bar.
- MP4s are streamed to disk, finalized on stop, then added to the library.
- Pausing excludes that interval from the recording.
- Closing or minimizing the meeting window stops and saves the current recording.
- All recording and playback happens locally. No account or service is needed.

FILES
Recordings/    Finished MP4s. Pending or failed files end in .recording.mp4;
               they are preserved for investigation and excluded from the library.
Logs/          Native capture diagnostics and app.log.
build.log      Compiler output from the most recent launch.
bin/           Locally compiled executable and runtime files.
RecorderApp.cs Native recorder UI/controller source (C# 5 / .NET Framework 4.8).
MainWindow.xaml Interface markup.
lib/           Released ScreenRecorderLib 7.0.1 x64 DLL and its MIT license.
Build.ps1      Builds without launching.
Start.cmd      Builds and launches.

A SHORT FIRST TEST
1. Start a meeting with a second participant or test account. Use headphones.
2. Record 15-30 seconds while both people speak and the meeting window changes.
3. Pause for several seconds, resume, speak again, then stop.
4. Check video, both voices, synchronization, and seeking in the saved recording.
5. Play audio in a different app during another short test. It should be absent
   when that app is outside the selected meeting audio process tree.
6. Restart the recorder and open the saved MP4 from the library.
7. Try minimizing the selected window: the recording should stop and save.

PROTOTYPE LIMITATIONS / VALIDATION
- The source and UI markup have been checked for syntax and control wiring.
  The bundled DLL has been checked for the capture API members used by the app.
- This environment blocks native process launches. The app has NOT been compiled
  or run here, and real Windows capture, audio sync, and playback are unverified.
  Start.cmd compiles locally; use the short test above before a real meeting.
- This is an unsigned prototype, not an installed production application.
- App audio isolation is by process tree, not by window. Browser audio can include
  other tabs. A separate browser instance/profile helps isolate a browser meeting.
- The selected app must actually render audio. Choosing an unrelated PID produces
  silence. The prototype never silently switches to recording all system audio.
- Headphones reduce sound from speakers leaking into the microphone.
- Window minimization is deliberately handled by saving and stopping; the app
  does not attempt background capture of minimized or protected content.
- A crash or forced shutdown before MP4 finalization may leave an unplayable file.
- The recording library is tied to this extracted folder. Move the folder as a
  whole if you want the videos and source to stay together.

TROUBLESHOOTING
Build fails: read build.log and confirm the full ZIP was extracted.
App fails to start: check bin/startup-error.log and install the VC++ runtime above.
No meeting sound: verify the meeting audio app/PID, play some meeting audio,
then record a short test. Browser audio may belong to its main browser process.
No microphone: refresh devices, select the correct input, and check Windows access.
Capture failure: check Logs/. Keep the window open and restore it before starting.
Playback failure: open the MP4 in Windows Media Player from the Recordings folder.

DEPENDENCY / REFERENCES
ScreenRecorderLib 7.0.1, MIT license:
https://github.com/sskodje/ScreenRecorderLib
https://www.nuget.org/packages/ScreenRecorderLib/7.0.1
Native audio API:
https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/
Native window capture API:
https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture
