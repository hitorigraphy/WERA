# WERA

GitHub Pages에서 동작하는 텍스트 게임 구동기입니다. C# / Blazor WebAssembly로 만듭니다.

## 사용법
1. 페이지에 게임팩(`.zip`)을 드래그 드롭합니다.
2. 새 게임을 시작하거나, 세이브(`.sav`, 내용은 JSON)를 드래그 드롭합니다.

## 게임팩 형식 (잠정)
TSV 파일 묶음 zip. 첫 줄은 헤더입니다.

| 파일 | 내용 |
|---|---|
| `pack.tsv` | `key`/`value` 행: `id`, `title`, `start` |
| `scenes.tsv` | `id`, `text` |
| `choices.tsv` | `scene`, `label`, `next` |
| `master/dungeon_master_v1.tsv` | (선택) `code`, `name` |
| `master/dungeon_flag_master_v1.tsv` | (선택) `dungeon_code`, `group_no`, `flag_code`, `condition_type`, `condition_value` |

### 던전 마스터
`master/dungeon_master_v1.tsv`가 있으면 던전 마스터로 읽습니다. 새 게임과 불러오기는 (튜토리얼이 구현될 때까지) 항상 홈 화면으로 들어갑니다. 세이브에 저장된 화면은 무시합니다.

- 던전 입장 조건은 `dungeon_flag_master_v1.tsv`에 적습니다. 같은 던전의 **같은 그룹은 AND, 그룹끼리는 OR**입니다. 조건이 없는 던전은 항상 입장할 수 있습니다.
- `condition_type`: `None`(조건 없음, 플래그 코드/밸류 생략 가능), `Equal`, `NotEqual`, `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual` (대소문자 무시). 값이 없는 플래그는 0입니다.
- 홈 화면의 `[101] 던전`은 입장 가능한 던전이 하나 이상일 때만 나타납니다. 클릭하거나 `101`을 입력하면 던전 선택 화면으로 갑니다. 화면의 클릭 가능한 텍스트는 모두 `[코드]`를 입력해도 동작합니다(`[999] 돌아가기`).
- 플래그는 세이브(`Flags`)에 저장됩니다.

예시 게임팩 원본은 [`samples/sample-pack`](samples/sample-pack)이고, 배포용 zip은 `src/Wera.Web/wwwroot/sample-pack.zip`입니다(원본을 고치면 zip도 다시 만드세요). 튜토리얼을 마친 상태의 세이브 예시는 `src/Wera.Web/wwwroot/sample-tutorial-clear.sav`입니다.

## 구조
- `src/Wera.Core` — 게임팩 로딩, 세션, 세이브 (브라우저 비의존)
- `src/Wera.Web` — Blazor WASM UI
- `tests/Wera.Core.Tests` — xUnit 테스트

## 개발
```
dotnet test tests/Wera.Core.Tests
dotnet run --project src/Wera.Web
```
`main`에 푸시하면 GitHub Actions가 Pages로 배포합니다 (저장소 설정 → Pages → Source: GitHub Actions).
