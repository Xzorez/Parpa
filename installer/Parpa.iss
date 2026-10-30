; Instalador de Parpa (Inno Setup 6).
; Por usuario y sin permisos de administrador: %LOCALAPPDATA%\Programs\Parpa.
;
;   iscc /DAppVersion=1.0.0 /DArch=x64 /DSourceDir=..\artifacts\publish-x64 installer\Parpa.iss
;
; Lo genera tools\build-installer.ps1 (y el flujo de GitHub Releases) a partir de "dotnet publish".

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish-" + Arch
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

; Repositorio de GitHub (propietario/nombre) para los enlaces de "Programas instalados".
#ifndef Repo
  #define Repo "Xzorez/Parpa"
#endif

#define AppName "Parpa"
#define AppExe "Parpa.exe"
#define AppUrl "https://github.com/" + Repo

[Setup]
; No cambiar nunca: identifica la instalación para actualizar y desinstalar.
AppId={{69656D3B-8A28-462D-9D42-35AD2663F018}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
; Windows 10 2004 o posterior, como dice la web.
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=Parpa-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\Parpa.App\Assets\Icon\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; Parpa se cierra a mano antes de copiar (ver [Code]); así no aparece el aviso de "aplicaciones en uso".
CloseApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
es.DesktopIcon=Crear un acceso directo en el escritorio
en.DesktopIcon=Create a desktop shortcut
es.SettingsShortcut=Ajustes de Parpa
en.SettingsShortcut=Parpa settings
es.LaunchParpa=Abrir Parpa
en.LaunchParpa=Open Parpa
es.RemoveData=¿Borrar también tus ajustes, notas, tareas y portapapeles de Parpa?%n%nSi vas a volver a instalarlo, elige No para conservarlos.
en.RemoveData=Also delete your Parpa settings, notes, tasks and clipboard history?%n%nIf you plan to reinstall, choose No to keep them.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autoprograms}\{cm:SettingsShortcut}"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; "Iniciar con Windows" lo escribe la propia app; al desinstalar se quita.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchParpa}"; Flags: nowait postinstall skipifsilent
; Actualización desde la app (instalación silenciosa con /RELAUNCH): Parpa vuelve a abrirse sola.
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: Relaunch

[Code]
function Relaunch(): Boolean;
begin
  Result := WizardSilent() and (Pos('/RELAUNCH', Uppercase(GetCmdTail())) > 0);
end;

procedure CloseParpa();
var
  ResultCode: Integer;
begin
  // Instancia única sin ventana principal: se cierra por nombre de proceso.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(400);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseParpa();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  CloseParpa();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent() then
    if MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{userappdata}\{#AppName}'), True, True, True);
end;
