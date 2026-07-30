# PhoneBridge for Windows

PhoneBridge is a beginner-friendly Windows launcher for the official
[scrcpy](https://github.com/Genymobile/scrcpy) Android screen mirroring and
control tool. It supports USB, Android 11+ wireless pairing, and the older USB-to-Wi-Fi
workflow.

> Use PhoneBridge only with a phone you own or have explicit permission to administer.
> It is for Android devices; it does not support iPhone/iOS.

## Download

Download `PhoneBridge.exe` from this repository's Releases page. No installer or
administrator rights are required. On first use it downloads the official scrcpy 4.1
Windows package (about 11 MB), verifies Genymobile's published SHA-256 checksum, and
installs it under `%LOCALAPPDATA%\PhoneBridge`.

Windows may show a SmartScreen warning because this hobby-project EXE is not
code-signed. Choose **More info → Run anyway** only if you downloaded it from this
repository and the checksum matches the release notes.

## USB setup

1. On the Android phone, open **Settings → About phone** and tap **Build number**
   seven times.
2. Open **Developer options** and enable **USB debugging**.
3. Connect the phone with a data-capable USB cable.
4. Unlock the phone and approve the USB debugging prompt.
5. Open PhoneBridge, click **Refresh devices**, then **Start mirroring**.

Some phone brands require their official Windows USB driver.

## Wireless setup — Android 11 or newer

1. Put the PC and phone on the same trusted Wi-Fi network.
2. On the phone, open **Developer options → Wireless debugging**.
3. Choose **Pair device with pairing code**.
4. Copy the shown phone IP, pairing port, and pairing code into PhoneBridge.
5. Click **Pair**.
6. Enter the connection port shown on the Wireless debugging screen, click
   **Connect over Wi-Fi**, refresh devices, and start mirroring.

Pairing and connection ports may be different.

## Legacy wireless setup

1. Connect and authorize the phone by USB.
2. Select it in PhoneBridge and click **Enable Wi-Fi (port 5555)**.
3. Find the phone's Wi-Fi IP address, enter it with port 5555, and click
   **Connect over Wi-Fi**.
4. After it connects, unplug the cable and start mirroring.

## Build from source

On Windows, run:

```powershell
.\build.ps1
```

The script uses the .NET Framework compiler included with Windows, builds the launcher,
and runs the included unit tests. The executable is written to `build\PhoneBridge.exe`.

## Third-party software

PhoneBridge downloads scrcpy 4.1 only from Genymobile's official GitHub release and
verifies SHA-256:

`5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db`

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for licensing.
