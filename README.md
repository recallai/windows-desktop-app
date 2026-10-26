# Windows Meeting Recorder

Record a meeting happening on your computer.

Requirements:
- Windows 11 x64 (ARM64 is not supported).
- Microsoft Visual C++ x64 Redistributable.
- Microphone access enabled for desktop apps in Windows Settings.
No API key, Node.js, or Visual Studio required.

How to run the app:
1. Download the repository 
2. Open the extracted MeetingRecorder folder and double-click Start.cmd. It builds and launches the app.
   
Record a meeting:
1. Open your meeting and click Refresh windows and devices in the recorder.
2. Select the meeting window, verify the meeting audio app, and choose your microphone.
3. Enter a meeting name and click Start recording.
4. Click Stop and save, then wait for saving to finish.
5. Play the recording in the app or click Open folder to find the MP4.

Doesn't include features like mute detection, tab isolation and automatic meeting detection. If you are interested in these features, you can build your own recorder with [Recall.ai's Desktop Recording SDK.](https://www.recall.ai/product/desktop-recording-sdk)
