Here is the refined `README.md`. I have polished the "Why use MicLock" section to sound more professional and impactful while keeping your specific scenarios at the forefront.

***

# MicLock

Stop letting other apps hijack your microphone volume.

MicLock is a lightweight, high-performance Windows utility designed for streamers, content creators, and power users who need absolute control over their audio input. It allows you to "lock" your microphone levels so that third-party applications (like Discord, Zoom, or gaming software) cannot change your volume settings without your permission.

<img width="731" height="747" alt="Screenshot" src="https://github.com/user-attachments/assets/4b302226-70b5-44fa-a4cc-424edee5fefc" />

## Key Features

*   **Volume Locking:** Fix your preferred microphone level and prevent other software from overriding it.
*   **Real-time Monitoring:** See live visual feedback of your microphone input levels for all connected devices.
*   **Instant Control:** Quickly Mute, Enable, or Disable any recording device directly from the interface.
*   **Change Notifications:** Get alerted immediately if another application tries to tamper with your microphone levels.
*   **Lightweight & Portable:** A single .exe file. No heavy installation, no bloatware.
*   **Set and Forget:** Optional "Run at Windows startup" ensures your settings are always active.
*   **Device Filtering:** Easily toggle between "Active Devices" or "All Devices" (including disconnected and disabled hardware).

## Why use MicLock?

Have you ever been in a voice chat with friends, only to have them constantly complain that your mic is too low or too high, even though you haven't touched a single setting? Or have you been in the middle of a stream or an important meeting, only to have your audio suddenly spike or drop because a background application changed your volume without your permission?

MicLock solves this. It puts you back in the driver's seat. Whether you are using standard hardware, complex virtual mixers (like SteelSeries Sonar), or simply voice chatting with friends, MicLock ensures your audio remains consistent exactly the way you configured it. 

It’s your microphone and your settings—nothing should change without your permission.


## Getting Started and Installation

> **No installer. No admin rights needed. Just a single .exe file that runs from anywhere.**

[![Download MicLock v1.0.0](https://img.shields.io/badge/Download-MicLock%20v1.0.0-blue?style=for-the-badge&logo=github)](https://github.com/froxel/MicLock/releases/download/v1.0.0/MicLock.exe)

1. **Download:** Click the button above to get the software.
2. **Move:** Move the `MicLock.exe` file to your desired folder.
3. **Run:** Launch `MicLock.exe`. 
    * *Note: This application does not require administrator rights to run.*
4. **Configure:** (Optional) Check "Run at Windows startup" to keep your locks active every time you boot.

## Build from Source

If you want to tinker with the code or build the latest version yourself, follow these steps:

### Prerequisites
*   Windows 10 or 11
*   .NET Framework 4 (Ensure you have the latest runtime installed)

### Instructions
1.  Clone this repository to your local machine.
2.  Navigate into the project folder.
3.  Run the build.bat file.
4.  The script will automatically compile MainLock.cs and use app.ico as the application icon.
5.  Your compiled .exe will be ready in the output folder.

## Contributing
Contributions, issues, and feature requests are always welcome! Feel free to fork the repo and submit a pull request.

## Attribution
*   **Icon:** The application icon was sourced from [Flaticon](https://www.flaticon.com/free-icons/keylocks).
