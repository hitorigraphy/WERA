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

예시는 [`samples/sample-pack`](samples/sample-pack)과 [`samples/sample-pack.zip`](samples/sample-pack.zip)을 보세요.

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
