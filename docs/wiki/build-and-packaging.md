# Build and Packaging

Monitor Swap은 .NET Framework 기반 Windows 앱입니다. 저장소에는 PowerShell 빌드 스크립트와 Inno Setup 패키징 스크립트가 포함되어 있습니다.

## Build

Debug 빌드:

```powershell
.\build.ps1 -Configuration Debug
```

Release 빌드:

```powershell
.\build.ps1 -Configuration Release
```

기본 출력 경로:

- Debug: `bin/Debug/MonitorSwap.exe`
- Release: `bin/Release/MonitorSwap.exe`

`build.ps1`은 .NET Framework C# compiler를 찾고, `Assets/MonitorSwap.ico`가 없으면 `tools/GenerateBrandAssets.ps1`을 먼저 실행합니다.

## Package

설치 파일 생성:

```powershell
.\package.ps1
```

버전을 직접 지정하는 경우:

```powershell
.\package.ps1 -Version 1.0.4
```

`package.ps1`은 Release 빌드를 만든 뒤 Inno Setup 6의 `ISCC.exe`를 사용해 `dist/`에 설치 파일을 생성합니다. 버전을 지정하지 않으면 `Properties/AssemblyInfo.cs`의 `AssemblyFileVersion` 값을 사용합니다.

## Notes

- `MonitorSwap.csproj`의 대상 프레임워크는 `.NET Framework 4.8`입니다.
- Inno Setup이 설치되어 있지 않으면 패키징 단계가 실패합니다.
- 문서만 변경한 경우에는 빌드를 생략할 수 있지만, 소스나 설치 스크립트를 바꾼 경우에는 관련 빌드를 실행하고 개발로그에 결과를 남깁니다.

