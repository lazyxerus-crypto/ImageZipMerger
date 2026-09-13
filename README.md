# ImageZipMerger

여러 ZIP·CBZ에 들어 있는 이미지를 원하는 순서대로 하나의 ZIP으로 합치는 Windows 프로그램입니다.

## 다운로드와 실행

[최신 배포 ZIP 다운로드](https://github.com/lazyxerus-crypto/ImageZipMerger/releases/latest)

배포 파일 `ImageZipMerger-v1.9.zip`을 모두 압축 해제한 뒤 `ImageZipMerger.exe`를 실행하세요. 같은 폴더의 `libvips-42.dll`이 필요합니다. 저장소의 **Code → Download ZIP**은 소스코드용입니다.

- Windows 64비트, .NET Framework 4.5 이상
- 실행파일은 코드 서명이 없어 Windows 스마트 앱 컨트롤에서 차단될 수 있습니다.

## 기능

- 이름이 서로 다른 ZIP·CBZ 파일 추가, 회차 이름 지정
- 회차 드래그 순서 변경, 긴 이름 가로 스크롤과 툴팁
- 회차 목록 빈 공간 드래그 선택, Ctrl/Shift 선택, Delete로 목록에서 제거
- 회차 선택 시 첫 이미지 자동 미리보기
- JPG, PNG, WebP, AVIF 등 이미지 미리보기
- 이미지 드래그 순서 변경, 다중 선택 이동, ↑/↓ 버튼 및 Alt+↑/↓ 단축키
- 회차·이미지 순서 변경 시 가로 스크롤 위치 유지
- 이미지 다중 선택 삭제, 복원 및 삭제 취소
- 삭제·복원 시 목록의 스크롤 위치 유지
- 원본 압축파일과 화질을 보존하며 연속 번호로 ZIP 저장

회차 제거는 원본 파일을 삭제하지 않습니다. 회차 제거 자체는 삭제 취소 대상이 아니며, 다시 추가할 수 있습니다. 작업 상태는 종료하면 사라집니다.

## 직접 빌드

Windows PowerShell에서 `./build.ps1`을 실행합니다. .NET Framework 컴파일러와 Node.js/npm이 필요합니다.
이미지 라이브러리는 고정 버전 `@img/sharp-win32-x64@0.35.4`에서 가져옵니다.
결과는 `build/ImageZipMerger-v1.9/`과 `build/ImageZipMerger-v1.9.zip`입니다.

GitHub Actions에서도 동일한 절차로 빌드하고 릴리스에 배포합니다.

## 타사 구성요소

이미지 디코딩에는 libvips를 사용합니다. 배포 ZIP에 타사 라이선스 고지를 포함합니다.
원본 및 빌드 정보: [libvips](https://github.com/libvips/libvips), [sharp-libvips](https://github.com/lovell/sharp-libvips), [sharp](https://github.com/lovell/sharp).

자세한 사용법은 [사용방법.txt](사용방법.txt)를 참고하세요.
