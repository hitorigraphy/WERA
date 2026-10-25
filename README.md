# WERA

GitHub Pages에서 동작하는 텍스트 게임 구동기입니다. C# / Blazor WebAssembly로 만듭니다.

## 사용법
1. 페이지에 게임팩(`.zip`)을 드래그 드롭합니다.
2. 새 게임을 시작하거나, 세이브(`.sav`, 내용은 JSON)를 드래그 드롭합니다.

## 게임팩 형식 (잠정)
TSV 파일 묶음 zip. 첫 줄은 헤더입니다.

**값의 타입:** 모든 코드(던전, 상점, 아이템, 캐릭터, 교환, 플래그 코드 등), 수량(어마운트, 소지제한), 캐릭터 수치(레벨, HP, XP 등), 플래그 값은 `long` 정수입니다. 그룹넘버(`group_no`)만 `int`입니다. 코드를 숫자가 아닌 이름으로 쓸 수 없습니다(예: 플래그 코드 `1001`). 게임팩 `id`는 문자열입니다.

| 파일 | 내용 |
|---|---|
| `pack.tsv` | `key`/`value` 행: `id`, `title` |
| `master/dungeon_master_v1.tsv` | (선택) `code`, `name` |
| `master/dungeon_flag_master_v1.tsv` | (선택) `dungeon_code`, `group_no`, `flag_code`, `condition_type`, `condition_value` |
| `master/shop_master_v1.tsv` | (선택) `code`, `name` |
| `master/shop_condition_master_v1.tsv` | (선택) `shop_code`, `group_no`, `flag_code`, `condition_type`, `condition_value` |
| `master/common_item_inventory_master_v1.tsv` | (선택) `item_code`, `item_name`, `carry_limit` |
| `master/bunch_item_inventory_master_v1.tsv` | (선택) `item_code`, `item_name`, `part_item_code`, `part_amount` |
| `master/character_inventory_master_v1.tsv` | (선택) `character_code`, `character_name`, `carry_limit`, `advantage_attribute_code`, `disadvantage_attribute_code` |
| `master/status_master_v1.tsv` | (선택) `status_code`, `status_name` |
| `master/character_status_master_v1.tsv` | (선택, 캐릭터 인벤토리/스테이터스 마스터 필요) `character_code`, `status_code`, `level`, `initial_value`, `min_value`, `max_value` |
| `master/attribute_master_v1.tsv` | (선택, 캐릭터 인벤토리가 있으면 필수) `attribute_code`, `attribute_name` |
| `master/status_effect_master_v1.tsv` | (선택) `status_effect_code`, `status_effect_name` |
| `master/status_effect_effect_master_v1.tsv` | (선택, 상태이상 마스터가 있어야 함) `status_effect_code`, `trigger_type`, `priority`, `effect_code`, `effect_value_1`, `effect_value_2`, `effect_value_3` |
| `master/equipment_slot_master_v1.tsv` | (선택) `equipment_slot_code`, `equipment_slot_name` |
| `master/tag_master_v1.tsv` | (선택) `tag_code`, `tag_name` |
| `master/unique_item_inventory_master_v1.tsv` | (선택) `item_code`, `item_name` |
| `master/unique_item_tag_master_v1.tsv` | (선택, 유니크 아이템 인벤토리/태그 마스터 필요) `item_code`, `tag_code` |
| `master/common_item_tag_master_v1.tsv` | (선택, 커먼 아이템 인벤토리/태그 마스터 필요) `item_code`, `tag_code` |
| `master/character_tag_master_v1.tsv` | (선택, 캐릭터 인벤토리/태그 마스터 필요) `character_code`, `tag_code` |
| `master/equipment_slot_tag_master_v1.tsv` | (선택, 캐릭터 인벤토리/장비 슬롯/태그 마스터 필요) `character_code`, `equipment_slot_code`, `tag_code`, `group_number`, `is_positive` |
| `master/shop_trade_master_v1.tsv` | (선택) `code`, `shop_code`, `cost_item_code`, `cost_amount`, `receive_item_code`, `receive_amount` + 선택 열 `cost_kind`, `receive_kind` |
| `master/shop_trade_condition_master_v1.tsv` | (선택) `trade_code`, `group_no`, `flag_code`, `condition_type`, `condition_value` |

### 아이템과 상점 교환
- **커먼 아이템 인벤토리**(`common_item_inventory_master`)는 아이템 코드, 이름, 소지제한(`carry_limit`, 한 번에 소지할 수 있는 최대 수량, 1 이상의 정수)입니다.
- **번치 인벤토리**(`bunch_item_inventory_master`)는 여러 아이템을 묶은 것으로, 같은 `item_code`의 행들이 하나의 번치가 됩니다(행마다 `item_name`은 같아야 함). `part_item_code`는 아이템 인벤토리의 아이템만 가리킬 수 있고, 번치에는 소지제한이 없으며, `part_amount`는 1 이상입니다.
- 아이템 코드는 커먼 아이템 인벤토리와 번치 인벤토리에서 겹치면 안 됩니다.
- 인벤토리 계통은 **커먼 아이템 / 유니크 아이템 / 번치 아이템 / 캐릭터** 네 종류입니다(`common_item_inventory_master`, `unique_item_inventory_master`, `bunch_item_inventory_master`, `character_inventory_master`).
- **캐릭터 인벤토리**(`character_inventory_master`)는 열이 `character_code`, `character_name`, `carry_limit`, `advantage_attribute_code`, `disadvantage_attribute_code`인 별도 마스터이고 코드 공간도 아이템과 따로입니다. 모든 캐릭터는 **유리속성**과 **불리속성**을 하나씩 가져야 하고, 둘 다 속성 마스터에 있어야 하며 서로 달라야 합니다.
- **스테이터스**(`status_master`)는 스테이터스의 종류(level, hp, xp, exp 등)이고, **캐릭터 스테이터스**(`character_status_master`)는 캐릭터 × 스테이터스 × 레벨별 값입니다. 복합키는 `(character_code, status_code, level)`이고 값은 `initial_value`, `min_value`, `max_value`(모두 정수)입니다. `min_value <= initial_value <= max_value`여야 하고, `level`은 0 이상입니다. **폴백**: 현재 레벨의 행을 먼저 찾고, 없으면 `level` 0의 행을 씁니다. 캐릭터를 만들 때는 레벨이 정해지지 않았으므로 `level` 0의 행을 읽습니다. 엔진이 예약한 스테이터스 코드는 **1 = level, 2 = hp, 3 = xp**입니다(이름은 자유). `status_master`가 있으면 **level(1)과 hp(2)는 반드시** 있어야 하고, `character_status_master`가 있으면 **모든 캐릭터에게 level과 hp의 `level` 0 행이 있어야** 합니다(어기면 로드 오류). xp(3)는 선택입니다. 캐릭터를 새로 얻을 때(`AddCharacter`) 상태가 레벨 0 행으로 채워집니다: 레벨 = level의 `initial_value`, HP = hp의 `initial_value`(최대 HP는 `max_value`), 경험치 = xp의 `initial_value`(필요 경험치는 `max_value`). **xp 행이 없으면 0/0**으로 시작합니다.
- **속성**(`attribute_master`)은 속성 코드와 이름입니다. 속성 아이콘 이미지는 아직 경로 규칙이 없어 `No Image`로 표시합니다.
- **상태이상**(`status_effect_master`)은 상태이상 코드와 이름입니다. 캐릭터에 걸려 있는 상태이상은 세이브에 저장하며(아래 "상태이상 저장"), 파티 설정 슬롯의 아이콘 줄에 아이콘으로 나옵니다.
- **임베디드 이펙트**는 TSV로 정의하는 마스터가 아니라 **구동기에 하드코딩된 클래스**입니다. `EmbeddedEffect`(추상 클래스)를 상속해서 만들고 `EmbeddedEffectRegistry.Default`(등록부)에 등록합니다.
  - **생성자**가 `(Character target, long value1, long value2, long value3)`를 받아 `Target`, `Value1~3`에 **저장**합니다.
  - `abstract long EffectCode`: 이펙트 코드.
  - `abstract void Execute()`: **파라미터 없이**, 저장해 둔 타겟과 밸류로 실행만 합니다.
  - 등록부에는 인스턴스가 아니라 `(이펙트 코드, 만드는 방법)`을 등록합니다. 예: `new(1, (t, a, b, c) => new HealEffect(t, a, b, c))`. `Create(코드, 타겟, 밸류 1~3)`로 이펙트를 만들면(실행하지 않음) 그 이펙트의 `Execute()`를 부릅니다. 만들어진 이펙트의 `EffectCode`가 등록한 코드와 다르면 오류입니다.
  - `Character`는 마스터 정의(`Entry`)와 바꿀 수 있는 현재 상태(`State`)를 함께 가집니다.
  - **내부 카운터와 유효 플래그:** 이펙트는 `Counter`(처음 0)와 `IsValid`(처음 true)를 가집니다. 이 둘은 익스큐터(`EffectExecutor`)만 바꿉니다.
  - **익스큐터:** `Run()`은 유효한 이펙트를 추가한 순서대로 한 번씩 `Execute()`하고, 실행할 때마다 카운터를 1 올립니다. 카운터가 **`Value2` 이상**이 되면 그 이펙트는 무효(`IsValid = false`)가 되어 다음 `Run()`부터 돌지 않고, 실행이 끝날 때 목록에서 폐기됩니다. 즉 **`Value2`는 최대 실행 횟수**입니다(0 이하여도 한 번은 실행됨). `Execute()`를 직접 부르면 카운터는 올라가지 않습니다.
  - **정의된 이펙트:**
    - `1` `HealEffect`: 타겟의 HP를 `Value1`만큼 늘립니다(음수면 줄임). HP는 0~최대 HP 안에 머뭅니다. `Value2`는 최대 실행 횟수, `Value3`은 쓰지 않습니다. 실행할 때 타겟이 이미 사망(HP 0 이하)했으면 HP를 바꾸지 않고 **조기 리턴**하며 이 이펙트를 **바로 무효**로 만듭니다(되살리지 않음). 이 이펙트의 실행으로 타겟이 죽은 경우에는 그 실행은 그대로 끝나고, 다음 실행 때 무효가 됩니다. 실행 횟수에 무제한은 없습니다(`Value2`가 0 이하여도 한 번만 실행).
- **상태이상 이펙트**(`status_effect_effect_master`)는 "상태이상(`status_effect_code`)이 `trigger_type` 시점에 임베디드 이펙트(`effect_code`)를 `effect_value_1~3`으로 실행한다"를 적습니다. 상태이상 코드는 상태이상 마스터에, 이펙트 코드는 임베디드 이펙트에 등록되어 있어야 하고 밸류는 비워 두면 0입니다. `priority`는 `long` 값이며 정렬 방향은 사용하는 쪽이 정합니다. `trigger_type`은 이름으로 적습니다(대소문자 무시, 숫자 불가).
  - `None`(조건 없음/시점 없음), `BeforeAttack`(공격하기 전), `AfterAttack`(공격한 후), `BeforeAttacked`(공격을 받기 전), `AfterAttacked`(공격을 받은 후), `AfterTurnStart`(턴 개시 후), `AfterTurnEnd`(턴 종료 후), `AfterBattleStart`(배틀 개시 후), `AfterBattleEnd`(배틀 종료 후), `AfterMove`(이동 후)
- `GameSession.ApplyEffect(이펙트 코드, 캐릭터 코드, 밸류 1~3)`은 이펙트를 만들어(타겟과 밸류 저장) `Execute()`로 보유 캐릭터에게 실행하고, 바뀐 상태를 세이브가 받아들이는 범위(레벨 1 이상, HP 0~최대 HP, XP 0~필요 XP)로 보정해 저장합니다.
- **장비**는 슬롯과 태그로 정합니다.
  - `equipment_slot_master`는 장비 슬롯의 종류(무기, 방어구 등), `tag_master`는 태그(검, 갑옷, 중량 등), `unique_item_inventory_master`는 유니크 아이템(`item_code`, `item_name`)입니다. 유니크 아이템의 코드 공간은 아이템/번치 인벤토리와 따로입니다.
  - `unique_item_tag_master`, `common_item_tag_master`, `character_tag_master`는 각각 유니크 아이템, 커먼 아이템(아이템 인벤토리), 캐릭터가 가진 태그입니다(대상과 태그가 각자의 마스터에 있어야 하고, 같은 `(대상 코드, tag_code)`를 두 번 쓸 수 없음). **장비 슬롯에는 이 세 종류가 모두 들어갈 수 있습니다**(예: 강령술사가 강시 캐릭터를 장비). 어느 종류가 들어가는지는 슬롯 태그 규칙에 그 종류의 태그를 쓰는 것으로 정합니다. **장비 가능 판정은 (장비하는 캐릭터의 태그 + 끼울 것의 태그)를 합친 태그 집합으로 슬롯 태그 규칙을 검사합니다**(캐릭터 태그는 `character_tag_master`).
  - `equipment_slot_tag_master`는 **캐릭터의 장비 슬롯에 어떤 장비가 들어가는가**를 태그로 적습니다. **복합키는 앞의 네 열**(`character_code`, `equipment_slot_code`, `tag_code`, `group_number`)이고 `is_positive`는 값입니다. 같은 키를 두 번 쓰면 `is_positive`가 달라도 오류입니다. `is_positive`는 `true`/`false`(대소문자 무시) 또는 `1`/`0`입니다.
  - **부합 규칙**: 같은 (캐릭터, 슬롯)에서 같은 `group_number`가 한 그룹이고 **그룹 안은 모두 만족(AND), 그룹끼리는 하나만 만족해도 됨(OR)**입니다. `is_positive`가 참이면 장비가 그 태그를 **가져야** 하고, 거짓이면 **가지면 안 됩니다**. 규칙이 없으면 아무것도 낄 수 없습니다.
  - **캐릭터가 가진 장비 슬롯**은 이 마스터에 그 캐릭터로 나오는 슬롯입니다(처음 나온 순서). 행이 없는 캐릭터는 장비 슬롯이 없습니다.
  - **장비는 보유해야 낄 수 있습니다.** 유니크 아이템은 **한 개가 인스턴스 하나**입니다. 세이브의 `UniqueItems`(인스턴스 ID → `{ItemCode, Tags}`, **인스턴스 ID는 ULID 문자열**), `CharacterTags`(캐릭터 코드 → 태그 목록), `EquippedItems`(캐릭터 코드 → (장비 슬롯 코드 → `{"Kind": "UniqueItem"|"CommonItem"|"Character", "Code": 코드}`))로 저장합니다. 유니크 아이템 종류는 `Code` 대신 `Instance`(인스턴스 ID)로 가리킵니다: `{"Kind": "UniqueItem", "Instance": "01H..."}`. **캐릭터와 유니크 아이템 인스턴스는 자기 태그를 가집니다**: 인스턴스를 만들 때(캐릭터는 처음 저장/변경할 때) 각자의 태그 마스터에서 복사하고, 게임 중에 태그가 추가되거나 사라질 수 있으며(예: 변신한 상태에서만 낄 수 있는 아이템), 세이브의 `Tags`/`CharacterTags`가 없으면 마스터에서 복사합니다. 커먼 아이템은 인스턴스가 없어 늘 마스터의 태그입니다. 태그가 바뀌어 끼운 것이 더는 슬롯 규칙에 맞지 않으면 장비에서 빠집니다. 커먼 아이템의 보유는 인벤토리 수량, 캐릭터의 보유는 보유 캐릭터입니다. 한 개는 한 곳에만 낄 수 있습니다. 끼워 둔 커먼 아이템은 상점 교환 비용으로 쓸 수 없고, 캐릭터는 자기 자신을 낄 수 없습니다. 장비 선택 화면의 항목 번호는 종류마다 코드 공간이 따로라서 1부터 차례로 붙습니다.
  - 낄 수 있으려면 ① 캐릭터가 보유 캐릭터이고 ② 장비가 그 슬롯의 태그 조건에 부합하며 ③ 남는 개수가 있어야 합니다. 슬롯에 다른 장비가 있으면 새 장비로 교체되고 빠진 장비는 보유로 돌아갑니다. 파티에서 빠져도 장착은 유지됩니다.
  - 장비 슬롯 목록(`용사의 장비`)은 `무기 : 철검`처럼 낀 장비를 보여 주고, 슬롯을 고르면 그 슬롯에 **지금 낄 수 있는 보유 장비**가 클릭 가능한 항목으로 나옵니다(지금 끼고 있는 것은 `(장착중)`). 항목을 고르면 장착하고 같은 캐릭터의 장비 슬롯 목록으로 돌아가며 결과 메시지가 나옵니다. 장비를 낀 슬롯에서는 맨 위에 **`[0] 해제`**가 나옵니다. 다른 곳에 끼어 남는 개수가 없는 장비는 목록에 나오지 않습니다.
  - 세이브를 불러올 때 검증합니다: 보유 장비와 장착 장비는 장비 마스터에 있어야 하고, 장착한 캐릭터는 보유 캐릭터, 슬롯은 그 캐릭터의 장비 슬롯, 장비는 그 슬롯 태그에 부합해야 하며, 장비마다 낀 개수가 보유 개수를 넘으면 안 됩니다.
- **속성 전투 규칙**(전투 시스템이 생기면 적용, 아직 코드에는 없음): 유리속성으로 공격하면 데미지와 명중률이 ×1.5, 불리속성으로 공격하면 ×0.5입니다. 피격 쪽은 공격 속성이 자신의 유리속성이면 ×0.5, 불리속성이면 ×1.5입니다. 두 보정은 곱하므로 유리속성 공격자가 불리속성 피격자를 치면 ×2.25입니다.
- **교환**(`shop_trade_master`)은 상점 `shop_code`에서 `cost_item_code`를 `cost_amount`개 내고 `receive_item_code`를 `receive_amount`개 받는 것입니다. 아이템 코드는 아이템/번치 인벤토리 어느 쪽이든 가능하고, 어마운트는 1 이상입니다. 상점과 아이템 코드는 해당 마스터에 있어야 합니다.
  - **종류 열**: 여섯 열 뒤에 `cost_kind`, `receive_kind`를 **둘 다** 붙일 수 있습니다(순서 고정). 값은 `common_item`(커먼/번치 아이템, 비우면 이것), `unique_item`(유니크 아이템), `character`(캐릭터)이고, 코드는 종류에 맞는 인벤토리 마스터에 있어야 합니다.
  - **유니크 아이템과 캐릭터는 코스트로도 리시브로도 어마운트가 1**이어야 합니다(아니면 로드 오류). **유니크 아이템을 받으면** 새 인스턴스(ULID)가 만들어지고(마스터 태그 복사), **내면** 장비 슬롯에 끼지 않은 인스턴스가 ID가 작은(먼저 얻은) 것부터 쓰입니다.
  - **캐릭터를 받으면** 레벨 0 스테이터스로 새 캐릭터가 되고(이미 보유하면 교환 실패), **내면** 보유에서 빠지며 파티 슬롯, 상태이상, 태그가 지워지고 끼고 있던 장비는 보유로 돌아갑니다(다른 캐릭터의 장비 슬롯에 끼워져 있으면 낼 수 없음).
  - 어느 쪽이든 모자라거나 불가하면 아무것도 바꾸지 않고 사유를 알립니다.
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
- 플래그 값은 화면에 표시하지 않으며, 인벤토리 확인 화면은 나중에 추가됩니다.
- 이 게임은 검은 바탕에 흰 글자, **등폭 폰트**를 씁니다.

### 파티 설정 (`[103]`, 홈에 조건 없이 항상 나옴)
- 슬롯 아래에 모드를 바꾸는 텍스트 버튼이 있습니다: **`[10] 캐릭터 배치`**, **`[11] 장비 착용`**(클릭하거나 코드 입력). 화면 제목에도 지금 모드가 나옵니다(`파티 설정 · 캐릭터 배치` / `파티 설정 · 장비 착용`).
  - **캐릭터 배치**: 아래에 나오는 방식으로 슬롯의 캐릭터를 배치합니다. 돌아가기는 홈입니다.
  - **장비 착용**: 같은 3×3 슬롯이 나오지만 캐릭터가 있는 슬롯만 고를 수 있습니다(빈 슬롯은 눌러지지 않음). 슬롯을 고르면 그 캐릭터의 **장비 슬롯 목록**(`○○의 장비`)으로 갑니다. 거기서 슬롯(`[슬롯코드] 무기` 등)을 고르면 그 슬롯에 낄 수 있는 **장비 가능 목록**(`○○의 무기`)이 나옵니다. 이 화면(장비 착용)에서 돌아가기를 하면 **파티 설정이 아니라 홈**으로 갑니다. 장비 슬롯 목록에서 돌아가면 장비 착용 화면, 장비 가능 목록에서 돌아가면 같은 캐릭터의 장비 슬롯 목록입니다.
- 3×3 슬롯입니다. 위 줄이 전위(슬롯 1~3), 가운데 줄이 중위(4~6), 아래 줄이 후위(7~9)입니다. 각 줄의 슬롯 위에 줄 이름(`전위`, `중위`, `후위`)이 구분선과 함께 한 줄로 나옵니다.
- 슬롯은 왼쪽에 캐릭터 아이콘, 오른쪽에 4줄을 보여 줍니다. 아이콘은 이 4줄 높이의 정사각형입니다.
  1. `Lv.5` (왼쪽 정렬) … `용사` (오른쪽 정렬)
  2. `HP : ****36/40***-`
  3. `XP : *----3/30----`
  4. 아이콘 줄: `[속성 아이콘 ⭕] [속성 아이콘 ❌]`. ⭕는 캐릭터의 유리속성, ❌는 불리속성입니다. 아이콘은 1줄 높이의 정사각형이고, 마우스를 올리면 속성 이름이 보입니다. 속성 배지 뒤에 걸려 있는 상태이상 아이콘이 걸린 순서대로 이어지고(마우스를 올리면 상태이상 이름), 이미지는 아직 No Image입니다. 빈 슬롯은 공란입니다.
- 게이지는 13칸이고, 채운 칸은 `*`, 빈 칸은 `-`이며 가운데에 `현재/최대`를 겹쳐 씁니다. 색은 50% 이상 흰색, 25% 이상 노랑, 그 아래 빨강이고 사망한 캐릭터는 회색입니다.
- 비어 있는 슬롯은 `빈 슬롯`, 회색 `0/0`입니다. 아이콘은 `No Image` 기본 이미지이고, 캐릭터 이미지 리소스가 없을 때도 이 이미지를 씁니다(지금은 항상).
- 슬롯의 이름이나 아이콘을 클릭하거나 슬롯 번호(1~9)를 입력하면 그 슬롯에 배치할 **캐릭터 목록**으로 갑니다. 흰색은 배치 가능, 회색은 배치 불가(보유하지 않음), 하늘색은 이 슬롯에 있는 자신, 초록은 이미 다른 슬롯에 배치된 파티원입니다.
  - 흰색을 고르면 그 슬롯에 배치합니다(원래 있던 캐릭터는 파티에서 빠짐).
  - 초록(배치된 파티원)을 고르면 위치를 서로 바꿉니다. 고른 슬롯이 비어 있으면 그 캐릭터가 이동합니다.
  - 하늘색(자신)을 고르면 그대로 파티 설정으로 돌아갑니다.
  - 캐릭터 선택 화면 맨 위의 **`[0] 비우기`**(클릭하거나 `0` 입력)를 고르면 그 슬롯의 캐릭터가 파티에서 빠지고 슬롯이 빈 슬롯이 되어 파티 설정으로 돌아갑니다. 캐릭터를 잃는 것은 아니고 여전히 보유 상태입니다. 선택한 슬롯이 비어 있으면 이 항목은 나오지 않습니다.
- 캐릭터 상태(레벨, HP, XP)와 파티 배치는 세이브의 `Characters`, `Party`에 저장합니다.
  - `Characters`: 캐릭터 코드 → `{ "Level", "Hp", "MaxHp", "Xp", "XpMax" }`. 코드는 캐릭터 인벤토리 마스터에 있어야 하고, 이 목록이 보유 캐릭터입니다.
  - `Party`: 길이 9의 배열(슬롯 순서). 빈 칸은 `null`. 보유한 캐릭터만 배치할 수 있고 같은 캐릭터를 두 번 배치할 수 없습니다.
  - `StatusEffects`: 캐릭터 코드 → 걸려 있는 상태이상 목록(**복구 가능한 값**). 상태이상마다 `StatusEffectCode`와 `Effects`(이펙트 행들)를 저장하고, 이펙트 행은 `Trigger`(이름), `Priority`, `EffectCode`, `Value1~3`, `Counter`, `IsValid`를 모두 가집니다. 걸린 시점의 마스터 값을 복사해 두므로 게임팩의 마스터가 나중에 바뀌어도 걸려 있던 이펙트는 저장한 값으로 복구됩니다.
    - 불러올 때 캐릭터는 보유 캐릭터여야 하고, 상태이상 코드는 상태이상 마스터에, 이펙트 코드는 임베디드 이펙트에 있어야 합니다. 같은 캐릭터에 같은 상태이상을 두 번 걸 수 없고, 트리거 이름이 올바라야 하며 카운터는 0 이상이어야 합니다.
    - 코드에서는 `GameSession.ApplyStatusEffect(캐릭터 코드, 상태이상 코드)`(이미 걸려 있으면 새로 걸어 갱신), `RemoveStatusEffect`, `StatusEffectsOf`로 다룹니다. 저장한 이펙트는 `ActiveEffect.Restore(등록부, 타겟)`로 카운터와 유효 플래그까지 그대로 가진 임베디드 이펙트 인스턴스로 되살리고, `ActiveEffect.Capture(...)`로 실행 중인 이펙트의 현재 값을 다시 저장용으로 뜹니다.
- 홈 화면에는 `[200] 세이브`(현재 상태를 `.sav`로 내려받기)와 `[300] 로드`(로드 화면에서 `.sav`를 드롭)도 항상 있습니다. 로드 화면과 던전 선택 화면의 돌아가기는 `[999]`입니다.
- 플레이어 상태는 세이브에 두 배열로 저장됩니다. 세이브 버전은 릴리스 전이라 1로 유지합니다.
  - 세이브 JSON의 코드 키(`"1001"` 등)는 정수를 문자열로 적은 것이고, `Party`의 코드는 숫자입니다. 숫자가 아닌 키는 세이브 오류입니다.
  - `PlayerFlags`: 플래그 코드 → `long` 값. 던전/상점/교환 조건이 이 값을 비교합니다.
  - `InventoryItems`: 아이템 코드 → 구조체 `{ "Amount": 수량 }`. 수량은 0 이상입니다.

예시 게임팩 원본은 [`samples/sample-pack`](samples/sample-pack)이고, 배포용 zip은 `src/Wera.Web/wwwroot/sample-pack.zip`입니다(원본을 고치면 zip도 다시 만드세요). 튜토리얼을 마친 상태의 세이브 예시는 `src/Wera.Web/wwwroot/sample-tutorial-clear.sav`입니다.

## 구조
- `src/Wera.Core` — 게임팩 로딩, 세션, 세이브 (브라우저 비의존)
- `src/Wera.Web` — Blazor WASM UI
- 마스터 데이터는 [MasterMemory](https://github.com/Cysharp/MasterMemory)(인메모리 읽기 전용 DB)에 올립니다. 테이블 정의는 `src/Wera.Core/Masters/MasterRows.cs`, 조회는 `GamePack.Masters.cs`(`GamePack.Masters`, `FindCharacter` 등)입니다. TSV 파싱과 외래키 검증은 기존 로더가 하고, 결과를 테이블로 올린 뒤 `Validate()`로 기본키 중복을 한 번 더 확인합니다. 테이블은 행 순서(int 순번)를 따로 기억하지 않고 기본키 순서로 저장되며, 순서가 필요한 조회(번치 부품, 상태이상 이펙트, 슬롯 규칙, 태그 등)는 키 순서대로 `OrderBy`/`ThenBy` 합니다. 화면에 나오는 목록(던전, 상점, 캐릭터 등)은 게임팩 파일 순서 그대로입니다. 조건표처럼 자연스러운 키가 없는 마스터는 모든 열이 복합키라 같은 행을 두 번 쓰면 오류입니다.
- `tests/Wera.Core.Tests` — xUnit 테스트

## 개발
```
dotnet test tests/Wera.Core.Tests
dotnet run --project src/Wera.Web
```
`main`에 푸시하면 GitHub Actions가 Pages로 배포합니다 (저장소 설정 → Pages → Source: GitHub Actions).
