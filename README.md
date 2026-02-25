# Server Picker X
<div align="center">

  <a href="https://api.github.com/repositories/1141835010/releases"><img src="https://img.shields.io/github/downloads/FN-FAL113/server-picker-x/total.svg"/></a>
  <img src="https://img.shields.io/github/license/FN-FAL113/server-picker-x"/>
  <img src="https://img.shields.io/github/v/release/FN-FAL113/server-picker-x"/>
  <img src="https://img.shields.io/github/stars/FN-FAL113/server-picker-x"/>

</div>

A lightweight server picker for CS2 and Deadlock with cross-platform support for **Windows** and **Linux**. A rewrite of my CS2 Server Picker to a modern MVVM pattern and service-based app. Designed to manage connections by blocking or unblocking specific servers based on geographic location.

## ⬇️ Download
### [Releases](https://github.com/FN-FAL113/server-picker-x/releases)

## 📷 Screenshot
![ServerPickerX](https://github.com/user-attachments/assets/a9c73ae6-525b-435c-b5ae-c7040838fb2f)
<details>
  <summary>Windows Short Demo</summary>
  
  [![Windows Short Demo Video](https://img.youtube.com/vi/RLgMeFtGVO4/0.jpg)](https://youtu.be/RLgMeFtGVO4)
</details>
<details>
  <summary>Linux (Arch) Short Demo</summary>
  
  [![Windows Short Demo Video](https://img.youtube.com/vi/dC2uMo_Upz8/0.jpg)](https://youtu.be/dC2uMo_Upz8)
</details>

## ❔ FAQ
**1. How it works, will I get banned?!**
 - The app does not modify any game or system files, I can assure you are safe from being banned when using the app as long as you do not download from untrusted sources. It will add necessary firewall policies to block game server relay ip addresses from being accessed by your network thus skipping them in-game when finding a match.

**2. Not being routed to lowest ping server or not working on your location?**
  - Due to the fact that we can only access and block **_IP relay addresses_** from valve's network points around the world rather than the game's actual server IP addresses directly, which are **_not exposed_** publicly, either your connection got relayed to the nearest available server due to **_how Steam Datagram relay works_** or **_your location might be a factor_**. 
- Re-routing can also happen anytime, even mid-game. One of the best ways to test it out is to block low-ping servers and leave out high-ping servers that are far from your current region. If your ping is high in-game, then you are being routed properly, and the blocked IP relays are not able to re-route you to a nearby server. I was able to test this out properly way back.
- Some solutions that might help out but are not guaranteed: turning off any vpn, uninstalling third-party antivirus and let windefender manage the firewall.
- ISP-related issues, such as bad routing or high ping, are out of scope and control since the app only adds firewall entries. Please contact your ISP instead.

**3. Why it requires admin/sudo permission on execution?<br>**
  - This is due to how Windows or Linux requires elevated execution when adding the necessary firewall policies. If the app is running in normal mode, it will not be able to do its operations and will throw errors.

**4. Windows smartscreen detected unrecognized app/publisher<br>**
  - The app requires a registered publisher which costs a lot of money. Rest assured the app is safe and has been tested already with more than 10k downloads.

![image](https://github.com/FN-FAL113/csgo-server-picker/assets/88238718/fe0af8a8-4195-457e-bbbf-3a772e7f646c)

**5. I'm receiving frequent timeouts when a match is being confirmed<br>**
  - You may have blocked too many servers, for optimal searching and relaying block only the necessary server relays.

## 🐛 Troubleshooting
| Symptom | Likely Cause | Fix |
|---------|---------------|-----|
| Firewall rules not applied or app won't open | App not run as admin or in a sudo env | - For windows: Run the app in administrator mode <br/> - For Linux: Add execute permission for the script `chmod +x ServerPickerX.sh` and execute the app using the provided bash script `./ServerPickerX.sh`,  |
| No servers appear | Internet blocked / API unreachable | Check your network and try again. |
| Ping shows ❌ for all | Server IPs are blocked or unreachable | Ensure you’re connected to the internet and that no other firewall is interfering. |

If you encounter a bug, open an issue on GitHub and include:
* Operating System
* Steps to reproduce
* Any info/error logs (the app writes them to server-picker-x.log)

## 🛠️ Building from Source
#### Clone the repo
```
git clone https://github.com/FN-FAL113/server-picker-x.git
cd server-picker-x
```
#### Restore NuGet packages
`dotnet restore ServerPickerX.slnx`
#### Build (debug)
`dotnet build ServerPickerX.slnx -c Debug`
Executable output: in `ServerPickerX/bin/Debug/net10.0/<runtime>/publish/`
#### Build (release)
`dotnet publish ServerPickerX.slnx -c Release -r win-x64`  # or linux-x64.
Executable output: `ServerPickerX/bin/Release/net10.0/<runtime>/publish/`
#### Run in debug mode
`dotnet run --project ServerPickerX/ServerPickerX.csproj`


## 🔽 Disclaimer
- This project or its author are not affiliated, associated, authorized, endorsed by valve, its affiliates or subsidiaries. Images, names and other form of trademark are registered to their respective owners.
- You are free to compile the project on Visual Studio, the zip/tar file provided here is a clean compilation of the binaries.
  
## 💖 Support the Project/Dev
- I develop stuff for free with dedication and hard work. Sharing this project with fellow gamers or giving it a star is a huge sign of appreciation!</br>
<a href="https://www.paypal.com/paypalme/fnfal113" target=_blank>
  <img src="https://raw.githubusercontent.com/stefan-niedermann/paypal-donate-button/master/paypal-donate-button.png" alt="Donate via Paypal" width="40%" />
</a>
