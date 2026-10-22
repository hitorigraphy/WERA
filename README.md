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
| `master/shop_master_v1.tsv` | (선택) `code`, `name` |
| `master/shop_condition_master_v1.tsv` | (선택) `shop_code`, `group_no`, `flag_code`, `condition_type`, `condition_value` |
| `master/item_inventory_master_v1.tsv` | (선택) `item_code`, `item_name`, `carry_limit` |
| `master/bunch_inventory_master_v1.tsv` | (선택) `item_code`, `item_name`, `part_item_code`, `part_amount` |
| `master/character_inventory_master_v1.tsv` | (선택) `character_code`, `character_name`, `carry_limit` |
| `master/shop_trade_master_v1.tsv` | (선택) `code`, `shop_code`, `cost_item_code`, `cost_amount`, `receive_item_code`, `receive_amount` |
| `master/shop_trade_condition_master_v1.tsv` | (선택) `trade_code`, `group_no`, `flag_code`, `condition_type`, `condition_value` |

### 아이템과 상점 교환
- **아이템 인벤토리**(`item_inventory_master`)는 아이템 코드, 이름, 소지제한(`carry_limit`, 한 번에 소지할 수 있는 최대 수량, 1 이상의 정수)입니다.
- **번치 인벤토리**(`bunch_inventory_master`)는 여러 아이템을 묶은 것으로, 같은 `item_code`의 행들이 하나의 번치가 됩니다(행마다 `item_name`은 같아야 함). `part_item_code`는 아이템 인벤토리의 아이템만 가리킬 수 있고, 번치에는 소지제한이 없으며, `part_amount`는 1 이상입니다.
- 아이템 코드는 아이템 인벤토리와 번치 인벤토리에서 겹치면 안 됩니다.
- **캐릭터 인벤토리**(`character_inventory_master`)는 열이 `character_code`, `character_name`, `carry_limit`인 별도 마스터이고 코드 공간도 아이템과 따로입니다. 아직 화면이나 세이브에서 쓰지는 않습니다.
- **교환**(`shop_trade_master`)은 상점 `shop_code`에서 `cost_item_code`를 `cost_amount`개 내고 `receive_item_code`를 `receive_amount`개 받는 것입니다. 아이템 코드는 아이템/번치 인벤토리 어느 쪽이든 가능하고, 어마운트는 1 이상입니다. 상점과 아이템 코드는 해당 마스터에 있어야 합니다.
- **교환 조건표**(`shop_trade_condition_master`)는 던전/상점 조건표와 같은 규칙(그룹 AND/OR, `None`)이고, 조건이 없으면 항상 가능합니다.

### 던전 마스터
`master/dungeon_master_v1.tsv`가 있으면 던전 마스터로 읽습니다. 새 게임과 불러오기는 (튜토리얼이 구현될 때까지) 항상 홈 화면으로 들어갑니다. 세이브에 저장된 화면은 무시합니다.

- 던전 입장 조건은 `dungeon_flag_master_v1.tsv`에 적습니다. 같은 던전의 **같은 그룹은 AND, 그룹끼리는 OR**입니다. 조건이 없는 던전은 항상 입장할 수 있습니다.
- `condition_type`: `None`(조건 없음, 플래그 코드/밸류 생략 가능), `Equal`, `NotEqual`, `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual` (대소문자 무시). 값이 없는 플래그는 0이고, `condition_value`는 `long` 범위의 정수입니다.
- 홈 화면의 `[101] 던전`은 입장 가능한 던전이 하나 이상일 때만 나타납니다. 클릭하거나 `101`을 입력하면 던전 선택 화면으로 갑니다. 화면의 클릭 가능한 텍스트는 모두 `[코드]`를 입력해도 동작합니다(`[999] 돌아가기`).
- 상점도 같은 방식입니다. 조건표(`shop_condition_master_v1.tsv`)의 그룹 규칙과 `condition_type`은 던전과 같고, 홈의 `[102] 상점가`는 열려 있는 상점이 하나 이상일 때만 나타납니다.
- 상점가 화면에는 열린 상점이 `[상점코드] 이름`으로 나오고, 클릭하거나 코드를 입력하면 그 상점의 **교환 목록**이 나옵니다. 목록에는 조건을 만족한 교환만 `[교환코드] 코스트 ×수량 → 받을것 ×수량` 형태로 보입니다(아이템은 이름으로). 교환 목록의 `[999]`는 상점가로, 상점가의 `[999]`는 홈으로 돌아갑니다. 교환 항목을 클릭하거나 교환 코드를 입력하면 교환이 **실행**됩니다.
  - 코스트 아이템이 인벤토리에서 줄고 리시브 아이템이 늘어납니다. 0개가 되면 인벤토리에서 지워집니다.
  - 코스트가 모자라거나 받은 뒤 수량이 아이템의 소지제한(`carry_limit`)을 넘으면 아무것도 바꾸지 않고 사유를 보여 줍니다(번치를 받을 때는 풀린 부품 각각에 적용). 조건을 만족하지 못한 교환은 코드를 입력해도 실행되지 않습니다.
  - 번치 아이템은 부품으로 풀어서 처리합니다. 받으면 `부품 × part_amount × 개수`가 인벤토리에 들어가고(번치 코드 자체는 들어가지 않음), 내려면 같은 만큼의 부품이 필요합니다.
  - 코스트는 받기 전의 보유량으로 판단합니다(같은 아이템이 양쪽에 있어도 마찬가지).
- 홈 화면에는 `[103] 파티 설정`이 조건 없이 항상 나옵니다. 화면 내용은 아직 없고 `[999] 돌아가기`만 있습니다. 플래그 값은 화면에 표시하지 않으며, 인벤토리 확인 화면은 나중에 추가됩니다.
- 홈 화면에는 `[200] 세이브`(현재 상태를 `.sav`로 내려받기)와 `[300] 로드`(로드 화면에서 `.sav`를 드롭)도 항상 있습니다. 로드 화면과 던전 선택 화면의 돌아가기는 `[999]`입니다.
- 플레이어 상태는 세이브에 두 배열로 저장됩니다. 세이브 버전은 릴리스 전이라 1로 유지합니다.
  - `PlayerFlags`: 플래그 코드 → `long` 값. 던전/상점/교환 조건이 이 값을 비교합니다.
  - `InventoryItems`: 아이템 코드 → 구조체 `{ "Amount": 수량 }`. 수량은 0 이상입니다.

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
