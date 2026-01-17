# AI Coding Assistant Skill Policy

이 정책은 스킬/도구 사용에 관한 세부 지침을 정의합니다.

## 1) 스킬/도구 사용 원칙
- 필요 시 사용 가능한 스킬/도구를 적극적으로 활용합니다.
- 클로드 스킬을 포함해 가용한 스킬을 우선적으로 검토합니다.
- GitHub Copilot Agent Skills를 활용하며, 프로젝트 스킬은 `.github/skills` 또는 `.claude/skills`에 두는 것을 기본으로 합니다.
- 검증된 공개 스킬(예: anthropics/skills 등)이 필요하면 우선 검토합니다.

## 2) ask_user 동작 규칙
- ask_user로 요청받은 경우, 사용자가 "종료"라고 명확히 커맨드할 때까지 대화를 종료하지 않습니다.

## 3) 기술 정책글 접근 방식
- 기술 정책글은 스킬을 통해 참조합니다.

## 4) Microsoft Learn MCP 활용
- 최신 기술/스펙 확인을 위해 Microsoft Learn MCP를 적극적으로 사용합니다.

## 5) Copilot Agent Skills 적용 범위
- Copilot coding agent, Copilot CLI, VS Code(지원되는 에디션)의 agent mode에서 Agent Skills를 우선 적용합니다.

## 6) 정책/스킬 자동 업데이트
- 프롬프트 또는 새 소스코드에서 스킬/도구 사용에 관한 신규 요구사항이 확인되면, 이 문서를 즉시 업데이트하고 이후 응답에 반영합니다.
