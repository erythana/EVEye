# EVEye
A Tool for Eve Online - quickly access Information from EVE-Players out of any local/chat window by copying their names.<br>
Still in early development, but should work just fine for now.

<img src="https://user-images.githubusercontent.com/42657063/232129804-52655df1-b49a-4597-becc-f13a431e6784.png" width=600px />

## Available on Windows, Linux (and MacOS*)
This tool is available on all major platforms.
It requires the [.NET 7 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) to be installed (except for Linux, it's bundled within the Flatpak).

* Windows:
  * Download the binary from here: tbd
* Linux:
  * Install the flatpak version like this: tbd
* MacOS:
    Run it with dotnet

### How to run the application with dotnet
Download the source code and extract the downloaded ZIP (click on the green "Code" button on top of this project).<br>
Run the command<br>
```
dotnet run
```
in the directory where the solution file (.sln) is located.

## Settings
You can configure settings in the appconfig.json file. Leave the Endpoints-Setting as is, these are for future tweaks if they should move for whatever reason.
You can change these settings:
* Theme (optional)
  * Either 'Dark', 'Light' or leave empty for system default
  * Defaults to system default
* ClipboardPollingMilliseconds (optional)
  * A value between 100 and 1000
  * Defaults to 250ms


## Some additional information
This tool queries the official ESI API and also the zKillboard API (credits to Squizz Caphinator) for information about the latest killboard activity of each player.<br>
It only accesses public availble information and doesn't share anything with anyone.<br>
This project is inspired by 'Pirates Little Helper' which is not maintained anymore and also not natively available on other platforms than Windows.

## Roadmap
Future improvements that are currently planned
* Caching/Cache-Invalidation
  * Right now every Clipboard-Copy queries the API, resulting in a delay, especialyl with a lot of people in local
* UI Improvements
  * Especially the Character-Tooltip with the Details needs some love
  * Persist window sizes after close

## If you have feature requests please let me know and send an Ingame-Mail to 'Christian Gaterau'
If you like this tool, consider donating some ISK.<br>

`
"Give me money. Money me! Money now! Me a money needing a lot now."
`