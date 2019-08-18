Name "Drawbridge"

!define VERSION_STRING "0.0.16"

!include "MUI2.nsh"
  
OutFile "DrawbridgeInstall.exe"

InstallDir "$PROGRAMFILES\Drawbridge"

ShowInstDetails show

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
	
!define MUI_FINISHPAGE_RUN "$INSTDIR\drawbridge.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start Drawbridge now?"
!define MUI_FINISHPAGE_BUTTON "Finish"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section

	SetOutPath $INSTDIR
 
	File UserInterface\bin\Debug\drawbridge.exe
	File UserInterface\bin\Debug\Harpocrates.dll
	File UserInterface\bin\Debug\WindowsService.exe
	File UserInterface\bin\Debug\Mono.Nat.dll
	
	createShortCut "$SMPROGRAMS\Drawbridge.lnk" "$INSTDIR\drawbridge.exe"
	
	WriteRegStr HKLM "Software\Drawbridge" \
                     "Version" "${VERSION_STRING}"

	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Drawbridge" \
					 "DisplayName" "Drawbridge"

	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Drawbridge" \
					 "DisplayVersion" "${VERSION_STRING}"
					 
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Drawbridge" \
					 "UninstallString" "$\"$INSTDIR\uninstaller.exe$\""

	WriteUninstaller $INSTDIR\uninstaller.exe
	
SectionEnd

Section "Uninstall"

	nsExec::Exec 'sc.exe stop Drawbridge'
	nsExec::Exec 'sc.exe delete Drawbridge'
	Delete $SMPROGRAMS\Drawbridge.lnk
	Delete $INSTDIR\Harpocrates.dll
	Delete $INSTDIR\drawbridge.exe
	Delete $INSTDIR\Mono.Nat.dll
	Delete $INSTDIR\WindowsService.exe
	Delete $INSTDIR\uninstaller.exe
	RMDir $INSTDIR
	
	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Drawbridge"
	DeleteRegKey HKLM "Software\Drawbridge"

SectionEnd
