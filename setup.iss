[Setup]
AppName=FlashFixer
AppVersion=1.0
DefaultDirName={autopf}\FlashFixer
DefaultGroupName=FlashFixer
OutputBaseFilename=FlashFixer-Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "bin\Release\net48\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FlashFixer"; Filename: "{app}\FlashFixer.exe"
Name: "{commondesktop}\FlashFixer"; Filename: "{app}\FlashFixer.exe"

[Run]
Filename: "{app}\FlashFixer.exe"; Description: "Launch FlashFixer"; Flags: postinstall nowait skipifsilent
