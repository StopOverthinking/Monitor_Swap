# Project Overview

Monitor Swap은 Windows 트레이에서 실행되는 WinForms 앱입니다. 선택한 모니터 사이에서 열린 창 배치를 왼쪽 또는 오른쪽으로 순환 이동시키고, 전역 단축키와 트레이 메뉴를 제공합니다.

## 주요 경로

- `Program.cs`: 앱 진입점입니다.
- `TrayApplicationContext.cs`: 트레이 아이콘, 메뉴, 설정 창, 회전 명령 연결을 담당합니다.
- `Forms/SettingsForm.cs`: 설정 UI입니다.
- `Models/AppSettings.cs`: 저장되는 앱 설정 모델입니다.
- `Services/WindowRotationService.cs`: 창 순환 이동의 핵심 로직입니다.
- `Services/MonitorDisplayService.cs`: 모니터 표시 정보와 선택 상태를 다룹니다.
- `Services/SettingsService.cs`: 설정 저장과 불러오기를 담당합니다.
- `Services/HotkeyManager.cs`: 전역 단축키 등록을 담당합니다.
- `Services/AppLocalization.cs`: 영어/한국어 UI 문구를 제공합니다.
- `Native/NativeMethods.cs`: Win32 API 호출 경계입니다.
- `Assets/`: 앱 아이콘과 설치 마법사 이미지가 있습니다.
- `installer/MonitorSwap.iss`: Inno Setup 설치 패키지 스크립트입니다.

## 작업 전 확인할 것

- 창 이동 동작을 바꿀 때는 `WindowRotationService`, `MonitorDisplayService`, `NativeMethods`를 함께 확인합니다.
- 설정 항목을 바꿀 때는 `AppSettings`, `SettingsService`, `SettingsForm`, localization 문자열을 함께 확인합니다.
- 릴리스나 설치 파일 작업은 `build.ps1`, `package.ps1`, `installer/MonitorSwap.iss`, `Properties/AssemblyInfo.cs`를 함께 확인합니다.

