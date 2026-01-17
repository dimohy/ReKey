# ReKey

ReKey는 Windows Scancode Map을 사용해 키를 영구적으로 리매핑하는 유틸리티입니다. 관리자 권한과 재부팅이 필요합니다.

## 주요 기능
- 간단한 UI로 키 리매핑
- 원본/대상 키 캡처
- 현재 매핑 목록 확인 및 삭제
- 진단 토글

## 빌드
- Visual Studio 2022+에서 `ReKey.sln`을 열고 `ReKey` 프로젝트를 빌드하세요.
- 대상 프레임워크: `net10.0-windows`

## 도구
- `tools/IconBuilder`는 `src/ReKey/Assets/ReKey.svg`를 `ReKey.ico`로 변환합니다.

## 라이선스
MIT License. 자세한 내용은 [LICENSE](LICENSE)를 참고하세요.
