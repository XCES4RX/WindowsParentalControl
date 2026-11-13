; ParentalControl Inno Setup Installer
; Requires Inno Setup 6.2+ (https://jrsoftware.org/isdownload.php)

#define MyAppName      "Parental Control"
#define MyAppVersion   "1.2.2"
#define MyAppPublisher "ParentalControl"
#define MyAppExeName   "ParentalControl.Admin.exe"
#define ServiceExeName "ParentalControl.Service.exe"
#define ServiceName    "ParentalControl.Service"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf}\ParentalControl
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=ParentalControlSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\admin\{#MyAppExeName}
SetupIconFile=..\icon.ico
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Service files
Source: "..\publish\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

; Admin app files
Source: "..\publish\admin\*"; DestDir: "{app}\admin"; Flags: ignoreversion recursesubdirs createallsubdirs

; Child countdown overlay & screenshot helper
Source: "..\publish\overlay\*"; DestDir: "{app}\overlay"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\admin\{#MyAppExeName}"; \
    IconFilename: "{app}\admin\{#MyAppExeName}"; Comment: "Launch Parental Control Admin"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\admin\{#MyAppExeName}"; \
    IconFilename: "{app}\admin\{#MyAppExeName}"; Comment: "Launch Parental Control Admin"

[UninstallRun]
; Stop and delete the service before files are removed
Filename: "sc"; Parameters: "stop {#ServiceName}"; \
    RunOnceId: "StopService"; Flags: runhidden waituntilterminated
Filename: "sc"; Parameters: "delete {#ServiceName}"; \
    RunOnceId: "DeleteService"; Flags: runhidden waituntilterminated

[UninstallDelete]
; Remove desktop shortcut
Type: files; Name: "{commondesktop}\{#MyAppName}.lnk"
; Remove ProgramData folder on uninstall (logs, database)
Type: filesandordirs; Name: "C:\ProgramData\ParentalControl"
; Remove entire install directory (Program Files\ParentalControl)
Type: filesandordirs; Name: "{app}"

[Code]
function ServiceExists(): Boolean;
var
    ResultCode: Integer;
begin
    Result := Exec('sc', ExpandConstant('query {#ServiceName}'),
                   '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
              and (ResultCode = 0);
end;

procedure PrepareForInstall();
var
    ResultCode: Integer;
    i: Integer;
begin
    // Terminate UI apps
    Exec('taskkill', '/F /IM ParentalControl.Overlay.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill', '/F /IM ParentalControl.Admin.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Stop service safely without deleting it
    if ServiceExists() then
    begin
        Exec('sc', ExpandConstant('stop {#ServiceName}'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        for i := 1 to 5 do
        begin
            Sleep(1000);
            if not ServiceExists() then Break;
        end;
    end;

    // Terminate any stuck service process to unlock files for replacement
    Exec('taskkill', '/F /IM ParentalControl.Service.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
end;

procedure SetupService();
var
    ResultCode: Integer;
    BinPath: String;
begin
    BinPath := ExpandConstant('"{app}\service\{#ServiceExeName}"');

    if not ServiceExists() then
    begin
        Exec('sc', ExpandConstant('create {#ServiceName}')
             + ' binPath= ' + BinPath
             + ' start= auto'
             + ' obj= LocalSystem'
             + ' DisplayName= "Parental Control Service"',
             '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

        Exec('sc', ExpandConstant('description {#ServiceName}')
             + ' "Monitors and enforces parental control time limits."',
             '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

        Exec('sc', ExpandConstant('failure {#ServiceName}')
             + ' reset= 86400 actions= restart/10000/restart/30000/restart/60000',
             '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end
    else
    begin
        Exec('sc', ExpandConstant('config {#ServiceName} binPath= ') + BinPath + ' start= auto',
             '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
end;

procedure StartService();
var
    ResultCode: Integer;
    Retries: Integer;
    Started: Boolean;
begin
    Started := False;
    Sleep(1500);

    for Retries := 1 to 4 do
    begin
        Log(Format('StartService: attempt %d of 4', [Retries]));
        if Exec('sc', ExpandConstant('start {#ServiceName}'),
                '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        begin
            if (ResultCode = 0) or (ResultCode = 1056) then
            begin
                Log('StartService: service started successfully');
                Started := True;
                Break;
            end;
            Log(Format('StartService: sc start returned %d', [ResultCode]));
        end;

        if Retries < 4 then
            Sleep(1000 + Retries * 1000);
    end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
    if CurStep = ssInstall then
    begin
        PrepareForInstall();
    end
    else if CurStep = ssPostInstall then
    begin
        SetupService();
        StartService();
    end;
end;
