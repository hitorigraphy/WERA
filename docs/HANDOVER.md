# WERA 인수인계 설명서 (게임팩 제작 세션용)

대상: 이 엔진 위에서 **본격적인 게임팩(`.wra`)을 새로 만들** 다음 세션. 이 문서는 혼자 읽어도 흐름이 잡히도록 쓴 **길잡이**이고,
각 마스터와 규칙의 **상세 명세는 [`README.md`](../README.md)** 가 정본입니다(아래에서는 README의 어느 대목을 보면 되는지 가리킵니다).
이 문서의 사실은 코드(`src/Wera.Core`)와 샘플 게임팩(`samples/sample-pack`)에서 직접 확인했습니다. README와 코드가 어긋난 곳은 부록 A에 따로 모았습니다 — **어긋나면 코드가 맞습니다.**

기준 시점: 브랜치 `develop`, 마지막 기능 커밋 `ece3e91`(시설 확인 화면), 테스트 1527개 통과, `SaveVersion = 1`.

---

## 1. 개요

### 1.1 WERA가 무엇인가
GitHub Pages에서 도는 **텍스트 게임 구동기**입니다(C# / .NET 10 / Blazor WebAssembly). 게임 내용은 전부 **게임팩**(TSV 묶음 zip, 확장자 `.wra`)이 정하고, 엔진은 게임팩을 읽어 검증한 뒤 화면과 규칙을 돌립니다.
플레이어는 페이지에 `.wra`를 드롭 → 새 게임을 시작하거나 세이브(`.wsv`, 내용은 JSON)를 드롭합니다.

게임의 큰 줄기: 홈(날짜/시간대) → 던전(격자 맵 이동·인카운트·전투) / 이벤트 / 교류 / 상점 교환 / 파티·장비·스킬 설정 / 장비 강화(합성·제련·개조) / **아지트**(층·시설·오퍼레이터·방어 파티) / **턴 종료**(방어전 → 시설 생산).

### 1.2 저장소 구조

| 경로 | 내용 |
|---|---|
| `src/Wera.Core` | 엔진 본체(브라우저 비의존): 게임팩 로더(`GamePack*.cs`), 세션/화면 상태(`GameSession*.cs`), 서킷(`*Circuit.cs`), 세이브 |
| `src/Wera.Web` | Blazor WASM UI. 화면 렌더링은 거의 전부 `Pages/Home.razor`, 스타일 `wwwroot/css`, 스크립트 `wwwroot/wera.js` |
| `src/Wera.Web/wwwroot/sample-pack.wra` | 배포용 예시 게임팩(원본은 `samples/sample-pack`을 zip한 것) |
| `src/Wera.Web/wwwroot/sample-tutorial-clear.wsv` | 튜토리얼을 마친 상태의 예시 세이브(테스트가 샘플 팩과 함께 읽음) |
| `samples/sample-pack` | 예시 게임팩 원본: `pack.tsv` + `master/*.tsv` 77개 + `image/*.png` 6개 |
| `tests/Wera.Core.Tests` | xUnit(약 95개 파일). 팩 로드 오류·서킷·화면 흐름까지 시험 |
| `proto/Wera.MasterMemoryProto` | MasterMemory 코드 생성용 보조(산출물 `bin/obj`만 있음) |
| `.github/workflows` | `test.yml`(develop 푸시/PR 때 build+test), `pages.yml`(main 푸시 때 test → publish → Pages 배포) |
| `README.md` | **게임팩 형식과 규칙의 정본**(아주 길다) |
| `WERA.slnx` | 솔루션(Core, Web, Tests) |

마스터 데이터는 [MasterMemory](https://github.com/Cysharp/MasterMemory)(인메모리 읽기 전용 DB)에 올립니다: 테이블 정의 `src/Wera.Core/Masters/MasterRows.cs`, 조회 `GamePack.Masters.cs`. 단, **TSV 파싱과 외래키 검증은 각 로더(`GamePack.*.cs`)가 하고**, 올린 뒤 `Validate()`가 기본키 중복을 한 번 더 확인합니다.

### 1.3 명령

```bash
dotnet test WERA.slnx                  # 전체 테스트(CI와 같음)
dotnet test tests/Wera.Core.Tests      # 코어 테스트만(pages.yml이 쓰는 형태)
dotnet run --project src/Wera.Web      # 로컬 실행
```

**모드를 묶은 바깥 팩 만들기:** `python tools/zip-wrapper-pack.py <폴더> <출력.wra>`(폴더에 `pack.tsv`, `master/pack_master_v1.tsv`, 중첩 `.wra`들; §4.1a).

**샘플 게임팩 다시 만들기**(원본 `samples/sample-pack`을 고쳤다면 반드시): 저장소 루트에서 `python tools/zip-sample-pack.py`를 돌립니다. `pack.tsv`, `master/*`, `image/**`를 최상위 폴더 없이 `sample-pack.wra`로 묶습니다.

**주의:** zip 안의 항목 이름은 정확히 `pack.tsv`, `master/<이름>_v1.tsv`(이미지는 이미지 마스터의 `file` 경로, 관례상 `image/…`)여야 합니다(최상위 폴더로 한 번 더 감싸면 안 됨; 구분자는 `/`, `\` 둘 다 찾음, 대소문자 구분). 윈도에서 폴더를 통째로 압축하면 최상위 폴더가 생겨 로드에 실패하니 `tools/zip-sample-pack.py`처럼 항목 이름을 직접 지정해서 만드세요.

### 1.4 브랜치·배포
- 작업은 **`develop`** 에서 하고 푸시합니다. 준비가 되면 **`main`으로 머지**하는 것이 곧 **Pages 배포**입니다(`pages.yml`: `main` 푸시 → `dotnet test` → `dotnet publish` → Pages).
- 지금까지의 방침: V1은 **게임팩이 완성되면** 확정합니다. 그전까지 `main` 머지는 하지 않았고, `SaveVersion`은 올리지 않습니다(§4.5).
- 커밋 메시지 끝에는 공동 작성자 줄(`Co-Authored-By: Claude …`)을 붙여 왔습니다.

### 1.5 규약

| 항목 | 규칙 |
|---|---|
| 파일 | `master/<이름>_v1.tsv`. UTF-8(BOM 허용), 탭 구분. 큰따옴표로 감싼 칸 안의 탭/줄바꿈과 `""` 이스케이프 지원(`Tsv.Parse`). 빈 줄은 무시 |
| 헤더 | **첫 줄은 헤더.** 로더는 헤더가 "필수 열 이름들을 이 **순서대로** 앞에 가진다"고 검사합니다(`Body`). 선택 열은 필수 열 뒤에 오며, 일부 마스터는 선택 열을 이름으로 찾아 순서가 자유(`character_inventory_master`의 `description`/`xp_type_code`, 커먼/유니크 아이템의 `skill_code`/`description`, 스킬의 `description` 등). 모자란 칸은 빈 칸으로 취급 |
| `memo` 열 | 어느 마스터든 헤더에 `memo` 열을 **어느 위치에나** 둘 수 있고 엔진은 읽지 않습니다(사람용 메모). 샘플 팩은 필수 행에 "필수"라고 적어 둠 |
| 타입 | 코드(던전·상점·아이템·캐릭터·플래그·태그·스킬…), 수량, 스테이터스 값, 플래그 값은 모두 **`long`**. **`group_number`만 `int`**. 코드는 숫자만(이름 불가). `pack.tsv`의 `id`만 문자열 |
| enum 값 | `flow_type`/`effect_type`/`tile_effect`/`condition_type`/`trigger_type` 등은 **이름**으로 적습니다(대소문자 무시, **숫자 불가**) |
| 비우기 | `value_1~3`/`param_1~3`은 비우면 0 |
| 줄바꿈 | 작업 트리의 `.cs` 등은 CRLF로 정규화됩니다(`git`이 LF→CRLF 경고를 냄). 스크립트로 파일을 고칠 땐 `newline=''`로 읽고 써서 줄바꿈을 보존하세요 |
| 오류 | 게임팩은 **로드할 때 전부 검증**하고 어기면 `PackFormatException`(한국어 메시지, 파일 이름과 위치 포함)으로 로드가 실패합니다. 세이브 쪽은 `SaveFormatException`. 게임 중에 잘못된 데이터를 만나 죽는 것보다 **로드에서 막는** 방침입니다 |
| 이름 규약 | 코드 번호 이름 공간은 마스터마다 별개이지만, **인벤토리 코드(커먼/유니크/번치/캐릭터)는 하나의 공간**입니다(§2.3) |

엔진이 정한 규칙(사용자 결정):
- **슬롯 규칙:** 모든 캐릭터는 슬롯 마스터(장비/스킬/부품/개조/오퍼레이터)의 **모든 슬롯을 가집니다**. 슬롯 태그 마스터는 슬롯의 유무가 아니라 "무엇을 낄 수 있는가"만 정합니다(규칙 행이 없는 슬롯에는 아무것도 못 낌).
- **속성 상성(결정만 됨, 전투에는 아직 미적용):** 유리속성 공격 ×1.5, 불리속성 공격 ×0.5, 피격 쪽은 유리속성이면 ×0.5·불리속성이면 ×1.5, 두 보정은 곱(최대 ×2.25). 배율은 게임 설정으로 바꿀 수 있습니다(§4.8).

---

## 2. 마스터 카탈로그

표기: **필수**는 `GamePack.RequiredMasterPaths`의 10개(+ `pack.tsv`). 나머지는 **선택**이지만, 선택 마스터가 있으면 그것이 가리키는 상위 마스터도 있어야 합니다. "키"는 기본키(복합이면 열 나열). 열은 샘플 팩의 헤더를 그대로 옮긴 것이고, **자세한 의미와 검증 규칙은 README 해당 항목**을 보세요.

### 2.0 `pack.tsv`
`key`/`value` 행(헤더는 필요 없음, 두 칸짜리 행들): `id`(필수, 문자열), `title`(필수), `debug_start_unique_items`(선택: 새 게임이 시작할 때 한 개씩 얻는 유니크 아이템 코드들, 쉼표 구분, 유니크 아이템 마스터에 있어야 함). 샘플은 `debug_start_unique_items = 8007`.

`master/pack_master_v1.tsv`(선택, **바깥 팩에만**)는 다른 게임팩들을 안에 넣어 합쳐 읽는 모드 기능의 목록입니다(`order_number`, `pack_file`; §4.1a). 일반 게임팩에는 필요 없습니다.

### 2.1 게임 설정·태그·스테이터스·속성

| 마스터 | 필수 | 키 | 열 | 용도 / 참조 |
|---|---|---|---|---|
| `game_setting_master` | 선택 | `setting_type` | `setting_type`, `value_type`(`Long`/`Decimal`/`Text`), `long_value`, `decimal_value`, `text_value` | 게임 전반 설정. 행이 없는 설정은 엔진 기본값. 값 종류와 최솟값은 설정마다 고정(어기면 로드 오류). 종류 목록은 §4.8 |
| `tag_master` | **필수** | `tag_code` | `tag_code`, `tag_name` | 태그 이름. **예약 태그**(플레이어/리더/메인 통화, 선택적으로 구금된 적·아군)는 게임 설정이 코드를 정하고 이 마스터에 있어야 함 |
| `status_master` | **필수** | `status_code` | `status_code`, `status_name`, (선택) `character_display_order` | 스테이터스 종류. **level(1), hp(2) 필수**. 예약 코드는 §4.9. `character_display_order` ≥1이면 캐릭터 상세에 그 순서로 표시 |
| `attribute_master` | **필수** | `attribute_code` | `attribute_code`, `attribute_name` | 속성. 캐릭터가 유리/불리속성을 하나씩 가짐 |
| `status_effect_master` | 선택 | `status_effect_code` | `status_effect_code`, `status_effect_name` | 상태이상 종류 |
| `status_effect_effect_master` | 선택(상태이상 마스터 필요) | `(status_effect_code, trigger_type, effect_code)` | `status_effect_code`, `trigger_type`, `priority`, `effect_code`, `effect_value_1`~`3` | 상태이상이 시점(`trigger_type`: `None`, `BeforeAttack`, `AfterAttack`, `BeforeAttacked`, `AfterAttacked`, `AfterTurnStart`, `AfterTurnEnd`, `AfterBattleStart`, `AfterBattleEnd`, `AfterMove`)에 실행할 **임베디드 이펙트**(코드에 하드코딩된 클래스. 지금은 `1 = HealEffect`뿐)를 가리킴 |
| `time_slot_master` | **필수** | `code` | `code`, `name` | 시간대(아침/낮 등) |
| `turn_master` | **필수**(시간대 필요) | `turn_count` | `turn_count`, `year`, `month`, `day`, `day_count`, `time_slot_code` | 달력과 턴. 게임은 가장 작은 `turn_count`에서 시작. `day_count`는 누계(날짜가 바뀌면 커져야 하고 같으면 같아야 함) |
| `xp_type_master` | 선택 | `xp_type_code` | `xp_type_code`, `xp_type_name`, `xp_type_abbr`(대문자 2글자), `xp_type_color`(`#RRGGBB`) | XP(MP/SP/TP…) 종류. 코드 0 행은 "XP 없음" 표기용 |

> **게임 설정으로 코드를 정하는 예약 태그**: `PlayerTagCode`, `LeaderTagCode`, `MainCurrencyTagCode`(행이 없으면 1, 2, 3), `CapturedEnemyTagCode`, `CapturedAllyTagCode`(행이 없으면 그 기능 없음). 다섯 코드는 서로 달라야 하고 태그 마스터에 있어야 합니다. 샘플은 1, 2, 3, 10, 11.

### 2.2 캐릭터

| 마스터 | 필수 | 키 | 열 | 용도 / 참조 |
|---|---|---|---|---|
| `character_inventory_master` | **필수** | `inventory_code` | `inventory_code`, `character_name`, `carry_limit`, `advantage_attribute_code`, `disadvantage_attribute_code`, (선택, 순서 자유) `description`, `xp_type_code` | 캐릭터 목록. **`carry_limit` = 같은 캐릭터를 가질 수 있는 최대 수**(1이면 유일한 특별 캐릭터). **캐릭터 1은 플레이어**(필수). 속성 둘은 속성 마스터에 있고 서로 달라야 함. `xp_type_code`는 XP 종류 |
| `character_status_master` | **필수** | `(character_code, status_code, level)` | `character_code`, `status_code`, `level`, `initial_value`, `min_value`, `max_value` | 캐릭터×스테이터스×레벨 값. **모든 캐릭터에 level과 hp의 `level 0` 행이 필요**. 현재 레벨 행이 없으면 레벨 0 행으로 폴백. `min ≤ initial ≤ max` |
| `character_tag_master` | **필수** | `(character_code, tag_code)` | `character_code`, `tag_code` | 캐릭터의 태그. **캐릭터 1은 플레이어 태그를 가져야 하고 플레이어 태그는 캐릭터 1만** 가질 수 있음 |
| `equipment_slot_master` | 선택 | `equipment_slot_code` | `equipment_slot_code`, `equipment_slot_name` | 장비 슬롯 종류(모든 캐릭터가 전부 가짐) |
| `equipment_slot_tag_master` | 선택 | `(character_code, equipment_slot_code, tag_code, group_number)` | `character_code`, `equipment_slot_code`, `tag_code`, `group_number`, `is_positive` | 그 캐릭터의 그 슬롯에 낄 수 있는 것의 태그 조건. **같은 그룹은 AND, 그룹끼리 OR**, `is_positive`가 참이면 그 태그를 가져야 하고 거짓이면 가지면 안 됨. 판정은 (낄 캐릭터의 태그 + 끼울 것의 태그) 합집합으로 |
| `skill_slot_master` | 선택 | `code` | `code`, `name` | 스킬 슬롯 종류 |
| `character_skill_slot_tag_master` | 선택 | `(character_code, skill_slot_code, group_number, tag_code)` | `character_code`, `skill_slot_code`, `group_number`, `tag_code`, `is_positive` | 스킬 슬롯 장착 규칙(위와 같은 그룹 규칙; 판정은 캐릭터 태그 + 스킬 태그) |
| `character_after_create_effect_master` | 선택 | `(character_code, execute_number)` | `character_code`, `execute_number`, `effect_type`, `value_1`~`3` | 캐릭터를 새로 만들 때 도는 서킷(§3). 초기 스킬은 여기서 `AddSkillToPool`/`SetSkillToSlot`로 정함 |

### 2.3 인벤토리·아이템·유니크 아이템

**인벤토리 코드는 하나의 공간**: 커먼 아이템, 유니크 아이템, 번치 아이템, 캐릭터 인벤토리의 코드는 모두 `inventory_code`이고 서로 겹칠 수 없습니다(같은 마스터 안에서도). 캐릭터 1은 플레이어라 아이템은 1번을 못 씁니다. **커먼 아이템 2는 메인 통화**(이름은 자유)로 반드시 있어야 하고 메인 통화 태그를 가집니다(그 태그는 아이템 2만).

| 마스터 | 필수 | 키 | 열 | 용도 / 참조 |
|---|---|---|---|---|
| `common_item_inventory_master` | **필수** | `inventory_code` | `inventory_code`, `item_name`, `carry_limit`, (선택, 순서 자유) `skill_code`, `description` | 커먼 아이템(인스턴스 없음, 수량만). `carry_limit` ≥ 1(소지 한도). `skill_code`가 있으면 전투 `[103] 아이템`으로 쓸 수 있음(쓰면 1개 줄어듦) |
| `common_item_tag_master` | **필수** | `(item_code, tag_code)` | `item_code`, `tag_code` | 커먼 아이템 태그(메인 통화 태그 포함) |
| `bunch_item_inventory_master` | 선택 | `(inventory_code, part_item_code)` | `inventory_code`, `item_name`, `part_item_code`, `part_amount` | 번치 = 여러 아이템 묶음(같은 코드의 행들). 받으면 부품으로 풀려 들어감, 소지 제한 없음 |
| `unique_item_inventory_master` | 선택 | `inventory_code` | `inventory_code`, `item_name`, (선택, 순서 자유) `skill_code`, `description` | 유니크 아이템(장비). 한 개가 인스턴스 하나(ULID). `skill_code`는 아이템이 가진 스킬 |
| `unique_item_tag_master` | 선택 | `(item_code, tag_code)` | `item_code`, `tag_code` | 유니크 아이템 태그(인스턴스가 복사해 가지며 게임 중 바뀔 수 있음) |
| `unique_item_status_master` | 선택 | `(item_code, status_code)` | `item_code`, `status_code`, `initial_value`, `min_value`, `max_value` | 유니크 아이템 스테이터스. **8번(`enhance_count`, 강화 횟수) 예약** |
| `unique_item_enhance_status_master` | 선택 | `(item_code, status_code, enhance_level)` | `item_code`, `status_code`, `enhance_level`, `value` | 강화 레벨별 스테이터스 가산. `EnhanceLevelMax`가 있으면 레벨 1~상한이 **모두** 적혀 있어야 로드됨 |
| `unique_item_after_create_effect_master` | 선택 | `(item_code, execute_number)` | `item_code`, `execute_number`, `effect_type`, `value_1`~`3` | 유니크 아이템 생성 후 서킷 |
| `part_slot_master` | 선택 | `code` | `code`, `name` | 장비의 **부품 슬롯** 종류(부품에는 커먼 아이템이 들어옴) |
| `part_slot_tag_master` | 선택 | `(item_code, part_slot_code, group_number, tag_code)` | `item_code`, `part_slot_code`, `group_number`, `tag_code`, `is_positive` | 그 장비의 그 부품 슬롯에 낄 부품의 조건(그룹 규칙 동일) |
| `mod_slot_master` | 선택 | `code` | `code`, `name` | **개조** 슬롯 종류 |
| `unique_item_mod_status_master` | 선택 | `(unique_item_code, mod_slot_code, item_code, status_code)` | `unique_item_code`, `mod_slot_code`, `item_code`, `status_code`, `value` | 그 장비의 그 개조 슬롯에 그 커먼 아이템을 끼우면 스테이터스에 더하는 값(끼울 수 있는 아이템은 이 마스터가 정함) |
| `unique_item_mod_skill_master` | 선택 | `(unique_item_code, mod_slot_code)`, 보조 `(unique_item_code, priority)` | `unique_item_code`, `mod_slot_code`, `priority`, `skill_code` | 개조 슬롯에 아이템이 끼면 쓸 수 있는 스킬(`priority` 작은 것이 우선) |
| `synthesis_flow_master` | 선택 | `execute_number` | `execute_number`, `flow_type`, `value_1`~`3` | 합성 서킷(§3) |
| `smelt_flow_master` | 선택 | `execute_number` | `execute_number`, `flow_type`, `value_1`~`3` | 제련 서킷(§3). 제련석은 게임 설정 `SmeltStoneItemCode` |
| `after_load_character_flow_master` | 선택 | `execute_number` | `execute_number`, `flow_type`, `value_1`~`3` | 세이브를 불러올 때 보유 캐릭터마다 도는 서킷 |
| `after_load_unique_item_flow_master` | 선택 | `execute_number` | `execute_number`, `flow_type`, `value_1`~`3` | 세이브를 불러올 때 보유 유니크 아이템마다 도는 서킷 |

### 2.4 스킬·전투·적·프리셋

| 마스터 | 필수 | 키 | 열 | 용도 / 참조 |
|---|---|---|---|---|
| `skill_master` | 선택 | `code` | `code`, `name`, (선택, 순서 자유) `main_target_type`, `sub_target_type`, `description` | 스킬. `main_target_type`: `Enemy`(기본), `Ally`, `Self`, `Any`, `AllyFallen`, `EnemyFallen`, `None`(직접 못 쓰는 패시브용). `sub_target_type`: `None`(기본), `SameRow`, `SameColumn`, `Right`, `Left`, `Back`, `Front`, `Cross`, `All`, `Random1`~`7`, 대각선들(README 스킬 마스터 항목) |
| `skill_tag_master` | 선택 | `(skill_code, tag_code)` | `skill_code`, `tag_code` | 스킬 태그(스킬 슬롯 규칙에 쓰임) |
| `skill_status_master` | 선택 | `(skill_code, status_code)` | `skill_code`, `status_code`, `initial_value`, `min_value`, `max_value` | 스킬 인스턴스의 스테이터스(스킬 경험치 등) 초깃값 |
| `skill_execute_effect_master` | 선택 | `(skill_code, execute_number)` | `skill_code`, `execute_number`, `effect_type`, `value_1`~`3` | **스킬 실행 서킷**: 스킬의 동작 |
| `skill_order_effect_master` | 선택 | `(skill_code, execute_number)` | 위와 같음 | **스킬 순서 서킷**: 행동 순서 점수(OrderPoint, 큰 쪽이 먼저) 계산 |
| `skill_auto_target_effect_master` | 선택 | `(skill_code, execute_number)` | 위와 같음 | **자동 타겟 서킷**: 적/쓰러진 메인 타겟 대신 고를 대상 결정 |
| `after_hit_effect_master` | 선택 | `execute_number` | `execute_number`, `effect_type`, `value_1`~`3` | 피격 후 서킷(범용) |
| `after_attack_effect_master` | 선택 | `execute_number` | 위와 같음 | 공격 후 서킷(범용) |
| `after_battle_win_effect_master` | 선택 | `execute_number` | 위와 같음 | 전투 승리 후 서킷(아군 유닛마다). **경험치/레벨업은 여기서** (§4.9) |
| `after_battle_lose_effect_master` | 선택 | `execute_number` | 위와 같음 | 전투 패배 후 서킷(아군 유닛마다). 구금된 아군 만들기(§4.7) |
| `after_battle_run_effect_master` | 선택 | `execute_number` | 위와 같음 | 도망 후 서킷(아군 유닛마다) |
| `xp_calculate_effect_master` | 선택(XP 종류 필요) | `(xp_type_code, execute_number)` | `xp_type_code`, `execute_number`, `effect_type`, `value_1`~`3` | XP 계산 서킷: 결과는 캐시 0번(XP 값)·1번(XP 최댓값) |
| `battle_master` | 선택 | `code` | `code`, `name`, (선택) `can_run` | 전투 정의. `can_run` false면 `[106] 도망` 불가 |
| `preset_unit_master` | 선택(캐릭터 필요) | `code` | `code`, `character_code` | 전투에 나올 유닛 하나가 어느 캐릭터인지 |
| `preset_unit_equip_master` | 선택 | `(preset_unit_code, equipment_slot_code)` | `preset_unit_code`, `equipment_slot_code`, `item_code` | 프리셋 유닛이 낀 장비(슬롯 태그 규칙에 맞아야 함) |
| `preset_unit_drop_master` | 선택 | `(preset_unit_code, item_code)` | `preset_unit_code`, `item_code`, `amount` | 유닛을 쓰러뜨리면 얻는 것(승리 시) |
| `preset_party_master` | 선택 | `code` | `code`, `name`, `front_left_unit_code`, `front_center_unit_code`, `front_right_unit_code`, `middle_left_…`, `middle_center_…`, `middle_right_…`, `back_left_…`, `back_center_…`, `back_right_unit_code` | 3×3 진형(칸은 프리셋 유닛 코드나 빈 칸; 유닛이 하나도 없으면 오류) |
| `battle_enemy_master` | 선택 | `(battle_code, preset_party_code)` | `battle_code`, `preset_party_code`, `weight` | 전투에 나올 수 있는 적 파티와 가중치(`weight` ≥ 1) |

### 2.5 던전·타일·이벤트·ADV·교류

| 마스터 | 필수 | 키 | 열 | 용도 / 참조 |
|---|---|---|---|---|
| `dungeon_master` | 선택 | `code` | `code`, `name`, (선택 셋) `entry_layer_number`, `entry_pos_x`, `entry_pos_y`, (선택) `encounter_rate` | 던전. 맵이 있는 던전은 도입 지점 필수. `encounter_rate` = 한 걸음당 전투 확률(%) |
| `dungeon_flag_master` | 선택 | `(dungeon_code, group_number, flag_code)` | `dungeon_code`, `group_number`, `flag_code`, `condition_type`, `condition_value` | 입장 조건(같은 그룹 AND, 그룹끼리 OR, 조건 없으면 항상 입장). `condition_type`: `None`, `Equal`, `NotEqual`, `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual` |
| `tile_master` | 선택 | `(code, tile_effect)` | `code`, `tile_effect`, `value_1`~`3` | 타일 효과(겹칠 수 있음): `None`0, `Wall`1, `Exit`2, `FixedBattle`3(v1=전투), `Adv`4(v1=ADV), `Warp`5(층·x·y), `Stairs`6, `Impassable`7, `Interact`8, `DungeonEvent`9(v1=던전 이벤트) |
| `dungeon_tile_master` | 선택 | `(dungeon_code, local_tile_text)` | `dungeon_code`, `local_tile_text`, `tile_code` | 던전 맵의 **한 글자**를 타일 코드에 잇는다 |
| `dungeon_map_master` | 선택 | `(dungeon_code, layer_number, pos_y)` | `dungeon_code`, `layer_number`, `pos_y`, `pos_x_0` … `pos_x_29` | 맵 한 줄(가로 30칸, 세로 30줄 고정). 빈 칸은 맵 밖 |
| `dungeon_layer_master` | 선택 | `(dungeon_code, layer_number)` | `dungeon_code`, `layer_number`, `name` | 층 이름(없으면 "맵 N") |
| `dungeon_encounter_master` | 선택 | `(dungeon_code, layer_number, battle_code)` | `dungeon_code`, `layer_number`, `weight`, `battle_code` | 맵별 인카운트 전투와 가중치(없으면 그 맵은 인카운트 없음) |
| `dungeon_event_master` | 선택 | `code` | `code`, `name` | **던전 이벤트**(이벤트 마스터와 별개 코드 공간, 조건 없음) |
| `dungeon_event_flow_master` | 선택 | `(dungeon_event_code, flow_number)` | `dungeon_event_code`, `flow_number`, `flow_type`, `param_1`~`3` | 던전 이벤트 흐름(이벤트 흐름 종류와 같고 `ReleaseCaptives` 포함) |
| `shop_master` / `shop_condition_master` | 선택 | `code` / `(shop_code, group_number, flag_code)` | `code`, `name` / `shop_code`, `group_number`, `flag_code`, `condition_type`, `condition_value` | 상점과 개점 조건 |
| `shop_trade_master` | 선택 | `code` | `code`, `shop_code`, `cost_item_code`, `cost_amount`, `receive_item_code`, `receive_amount`, (선택, 둘 다) `cost_kind`, `receive_kind` | 교환. 종류 값 `common_item`/`unique_item`/`character`(비우면 코드가 속한 마스터로 판단). 유니크 아이템과 캐릭터는 수량 1 |
| `shop_trade_condition_master` | 선택 | `(trade_code, group_number, flag_code)` | `trade_code`, `group_number`, `flag_code`, `condition_type`, `condition_value` | 교환 조건 |
| `event_master` | 선택 | `code` | `code`, `name` | 이벤트 |
| `event_condition_master` | 선택(이벤트 필요) | `(event_code, group_number, flag_code)` | `event_code`, `group_number`, `flag_code`, `condition_type`, `condition_value` | 이벤트가 열리는 조건(없으면 늘 열림) |
| `event_flow_master` | 선택(이벤트 필요) | `(event_code, flow_number)` | `event_code`, `flow_number`, `flow_type`, `param_1`~`3` | 이벤트 흐름(§3.2) |
| `adv_master` | 선택 | `code` | `code`, `name` | ADV(대사 장면) |
| `adv_flow_master` | 선택(ADV 필요) | `(adv_code, flow_number)` | `adv_code`, `flow_number`, `flow_type`, `param_1`~`3`, `text`, `force_name` | ADV 흐름(§3.2). `text`는 BBCode 일부(`[b]`, `[color=…]`, `\n`)와 상자 크기 검증이 있음 |
| `communication_group_master` | 선택 | `code` | `code`, `name` | 교류 그룹(코드 ≥ 1, 화면에 `[코드×100]`) |
| `communication_master` | 선택(그룹 필요) | `code` | `code`, `group_code`, `name`, `is_assist` | 교류. `is_assist` 참이면 파트너 대신 어시스트 캐릭터가 상대 |
| `communication_solo_flow_master` | 선택(교류 필요) | `(communication_code, character_code, flow_number)` | `communication_code`, `character_code`, `flow_number`, `flow_type`, `param_1`~`3`, `text` | 상대 캐릭터 한 명용 흐름 |
| `communication_duo_flow_master` | 선택(교류 필요) | `(communication_code, partner_code, assist_code, flow_number)` | `communication_code`, `partner_code`, `assist_code`, `flow_number`, `flow_type`, `param_1`~`3`, `text` | 파트너+어시스트 조합용 흐름 |
| `communication_default_flow_master` | 선택(교류 필요) | `(communication_code, flow_number)` | `communication_code`, `flow_number`, `flow_type`, `param_1`~`3`, `text` | 기본 흐름. **찾는 순서: 듀오 → 솔로 → 기본**(첫 번째로 흐름이 있는 마스터의 묶음 전체를 씀) |

> **없어졌거나 이름이 바뀐 파일**(있으면 로드 오류): `event_flow_condition_master`, `adv_flow_condition_master`("더 이상 쓰이지 않습니다"; 조건은 `JumpFlag*`로), `communication_flow_master`(→ `communication_solo_flow_master`).

### 2.6 아지트·시설·오퍼레이터·방어

| 마스터 | 필수 | 키 | 열 | 용도 / 참조 |
|---|---|---|---|---|
| `facility_master` | 선택 | `code` | `code`, `name` | 시설 종류 |
| `facility_condition_master` | 선택(시설 필요) | `(facility_code, group_number, flag_code)` | `facility_code`, `group_number`, `flag_code`, `condition_type`, `condition_value` | **설치 조건**(없으면 늘 지을 수 있음) |
| `facility_build_master` | 선택(시설·커먼 아이템 필요) | `(facility_code, group_code, item_code)` | `facility_code`, `group_code`, `item_code`, `amount` | 짓는 재료(같은 그룹 AND, 그룹끼리 OR; **행이 없으면 재료 없이 바로 지어짐**) |
| `hideout_floor_master` | 선택(시설 필요) | `floor_order` | `floor_order`, `facility_code` | 층이 생긴 **순서**(1 = 처음부터 있던 층)별 **기본 시설** |
| `hideout_expand_master` | 선택(커먼 아이템 필요) | `(expand_count, group_code, item_code)` | `expand_count`, `group_code`, `item_code`, `amount` | 아지트 확장 재료(첫 확장이 `expand_count` 1; 다음 번호 행이 없으면 더 확장 불가) |
| `operator_slot_master` | 선택 | `code` | `code`, `name` | 오퍼레이터 슬롯 종류 |
| `facility_operator_slot_master` | 선택(시설·슬롯 필요) | `(facility_code, operator_slot_code)` | `facility_code`, `operator_slot_code` | 시설이 가진 오퍼레이터 슬롯 |
| `operator_slot_tag_master` | 선택 | `(operator_slot_code, tag_code)` | `operator_slot_code`, `tag_code` | 슬롯에 서려면 캐릭터가 **모두** 가져야 하는 태그(행이 없으면 누구나) |
| `facility_flow_master` | 선택(시설 필요) | `(facility_code, flow_number)` | `facility_code`, `flow_number`, `flow_type`, `param_1`~`3` | **시설 서킷**: 턴 종료 때 생산(§3.2) |
| `operator_slot_defense_flow_master` | 선택(슬롯 필요) | `(operator_slot_code, flow_number)` | `operator_slot_code`, `flow_number`, `flow_type`, `param_1`~`3` | **방어력 계산 서킷**: 그 슬롯의 오퍼레이터 1명의 최대 방어력 |
| `facility_defense_master` | 선택(시설 필요) | `facility_code` | `facility_code`, `defense` | 시설 자체의 방어력(없으면 0) |
| `hideout_defense_enemy_master` | 선택(프리셋 파티 + `ThreatFlagCode` 필요) | `code` | `code`, `preset_party_code`, `min_threat`, `consume_threat`, `priority`, `chance` | **방어전 적 파티**(위협도·확률·우선순위; §4.6) |

### 2.7 서브모듈 마스터(서킷 종류마다 하나, 모두 선택)
이름 규칙: 이펙트(흐름) 마스터 이름에서 `_effect_master` 또는 `_flow_master`를 뗀 것 + `_submodule_master_v1.tsv`. 전체 목록과 열은 §3.4.

### 2.7a 이미지 (`master/image_master_v1.tsv`, 선택)

열 `image_code, kind, target_code, variation_number, file`(+메모). 키는 `image_code`, `(kind, target_code, variation_number)`도 유일해야 합니다. 로더는 `src/Wera.Core/GamePack.Image.cs`, 조회는 `Pack.ImageFor(kind, target, variation = 0)`(그 변형 → 변형 0 → null 순서로 찾고, 없으면 화면이 `No Image`를 그림).

| kind | target_code가 가리키는 마스터 | 쓰이는 곳 |
|---|---|---|
| `Character` | 캐릭터 인벤토리 코드 | 파티·홈 슬롯, 캐릭터 상세, ADV 말하는 사람(`variation_number`) |
| `UniqueItem` / `CommonItem` | 유니크 아이템 / 커먼 아이템 코드 | 장비·아이템 상세, 장비 슬롯 |
| `Skill` | 스킬 코드 | 스킬 상세 |
| `StatusEffect` | 상태이상 코드 | 배지 |
| `Attribute` | 속성 코드 | 배지 |
| `Facility` | 시설 코드 | 아지트 시설 슬롯 |

`file`은 zip 안의 경로(대소문자 구분, 앞 `/`·`\`·`..`·빈 조각 불가, 확장자 png/jpg/jpeg/webp/gif, 파일당 2 MB 이하 `GamePack.MaxImageBytes`)입니다. 관례상 `image/…`에 둡니다. 같은 캐릭터 코드의 모든 인스턴스는 같은 이미지를 씁니다. 대상 마스터가 팩에 없는 kind는 쓸 수 없고, 마스터가 가리키지 않는 zip 안의 파일은 읽지 않습니다. 아지트 층 아이콘과 빈 보스 슬롯은 마스터 코드가 없어 늘 `No Image`입니다.

### 2.8 참조 관계(핵심만)

```
pack.tsv(id,title)
game_setting_master ──(태그 코드 5종, SmeltStoneItemCode, ThreatFlagCode)──▶ tag_master / common_item_inventory_master / 플레이어 플래그
tag_master ◀── character_tag / common_item_tag / unique_item_tag / skill_tag / operator_slot_tag / *_slot_tag(규칙)
attribute_master ◀── character_inventory_master ──▶ xp_type_master
status_master ◀── character_status / unique_item_status / skill_status / (이펙트의 status_code 인자)
time_slot_master ◀── turn_master
inventory 코드 공간 = common_item ∪ unique_item ∪ bunch_item ∪ character_inventory
   ◀── shop_trade, 이벤트·ADV·교류·시설 흐름의 AddInventory/JumpInventory*, preset_unit_equip/drop
skill_master ◀── (common/unique 아이템의 skill_code), skill_*_effect, skill_tag, unique_item_mod_skill,
                 character_after_create(AddSkillToPool), 스킬 슬롯 규칙
preset_unit ──▶ character_inventory;  preset_party ──▶ preset_unit;  battle_enemy ──▶ battle + preset_party
battle_master ◀── 이벤트 흐름 Battle, tile_master FixedBattle, dungeon_encounter
adv_master ◀── 이벤트 흐름 Adv, tile_master Adv
dungeon_event_master ◀── tile_master DungeonEvent;  dungeon_tile ──▶ dungeon + tile_master;  dungeon_map ──▶ dungeon_tile
facility ◀── hideout_floor / facility_build / facility_operator_slot / facility_flow / facility_defense / facility_condition
operator_slot ◀── facility_operator_slot / operator_slot_tag / operator_slot_defense_flow
hideout_defense_enemy ──▶ preset_party;  (+ ThreatFlagCode 설정)
```

---

## 3. 서킷

**서킷** = 마스터 한 묶음(행의 `execute_number`/`flow_number` 순서)을 엔진의 실행기가 차례로 실행해 게임 동작을 게임팩이 정의하게 하는 장치입니다. 두 부류가 있고 **서로 코드를 나누지 않습니다**(재사용보다 단순함을 택함).

### 3.1 이펙트 서킷 (`UnitCircuitBase` 계열, 15종)

공통 모델(`src/Wera.Core/UnitCircuitBase.cs`):
- 서킷은 **시전자(`Caster`), 메인 타겟(`MainTarget`), 서브 타겟들(`SubTarget`)** 이라는 **파티 유닛**(캐릭터 + 장비)을 가집니다. 이펙트는 스테이터스를 유닛에 직접 쓰지 않고 먼저 캐릭터/장비(유니크 아이템)/스킬을 **포커스**한 뒤 포커스한 것과 **캐시**(`CacheValues`, `List<decimal>`, 번호 0~255 = `GamePack.MaxCacheIndex`)를 주고받습니다.
- **소수 버퍼링**: 서킷 안의 스테이터스는 `decimal`로 들고 있다가 끝날 때 **정수(소수는 버림)** 로, 각 스테이터스의 `min~max`(캐릭터는 현재 레벨 행, 없으면 레벨 0 행)로 맞춰 저장합니다. 바뀐 인스턴스만 되돌립니다.
- **점프는 뒤로만**: `Jump`/`JumpCache*`의 대상은 같은 소유자(스킬/캐릭터/아이템별 마스터는 같은 코드)의 **자신보다 큰 `execute_number`** 여야 하고 아니면 로드 오류 → 무한 반복이 없습니다. `Finish`는 서킷 전체를 끝냅니다.
- 공통 이펙트 종류 **`EffectType` 0~32** (모든 이펙트 서킷의 enum이 같은 번호와 이름을 가집니다):

| 번호 | 이름 | 인자 요약 |
|---|---|---|
| 0 | `None` | 없음 |
| 1~3 | `FocusCasterCharacter`, `FocusMainTargetCharacter`, `FocusSubTargetCharacter` | 3번은 `value_1` = 서브 타겟 번호(0부터) |
| 4~6 | `FocusCasterEquipment`, `FocusMainTargetEquipment`, `FocusSubTargetEquipment` | `value_1` = 장비 슬롯(6번은 `value_1` 서브 타겟 번호, `value_2` 장비 슬롯). 낀 **유니크 아이템**만 포커스 |
| 7~9 | `FocusCasterSkill`, `FocusMainTargetSkill`, `FocusSubTargetSkill` | `value_1` = 스킬 슬롯(9번은 `value_1` 서브 타겟, `value_2` 스킬 슬롯) |
| 10~12 | `CacheToFocusedCharacterModValue`, `…SetValue`, `FocusedCharacterValueToCache` | `value_1` = status_code, `value_2` = cache_index(0~255) |
| 13~15 | 같은 구성의 `…FocusedItem…` | 유니크 아이템 스테이터스 |
| 16~18 | 같은 구성의 `…FocusedSkill…` | 스킬 스테이터스 |
| 19~22 | `CacheAdd`, `CacheSubtract`, `CacheMultiply`, `CacheDivide` | `cache[v1] = cache[v2] ○ cache[v3]` (0으로 나누기 등은 아무 일도 안 함) |
| 23 | `SetCacheValue` | `cache[v1] = v2 / 10^v3` (v3 = 소수 자릿수 0~28) |
| 24 | `Jump` | v1 = 점프할 execute_number |
| 25 | `Finish` | 서킷 끝 |
| 26~31 | `JumpCacheEqual`, `NotEqual`, `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual` | `cache[v1]` vs `cache[v2]`, 참이면 v3으로 |
| **32** | **`JumpToSubmodule`** | v1 = submodule_code (§3.4) |

종류별 **고유 이펙트**(33부터; 서브모듈 도입으로 32가 공통이 되어 한 칸씩 밀림):

| 서킷 | 고유 종류 |
|---|---|
| 전투 패배 후 | `CaptureFocusedCharacter` 33, `JumpNotInDungeon` 34 (v1 = 점프할 번호) |
| 캐릭터 생성 후 | `AddSkillToPool` 33 (v1 = 스킬 코드, 만든 스킬을 포커스), `SetSkillToSlot` 34 (v1 = 스킬 슬롯 코드) |
| 스킬 자동 타겟 | `SubTargetToResult` 33 (v1 = 후보 번호; 마지막으로 놓은 값이 메인 타겟) — 이 서킷에는 `CacheToFocused*` 계열이 없음 |
| 스킬 순서 | `CacheToResult` 33 (`Result = cache[v1]`; Result가 OrderPoint) — 이 서킷에는 `CacheToFocused*` 계열이 없음 |
| 제련 | `FocusSmeltTarget` 33, `DeleteSmeltTarget` 34, `ConsumeSmeltStone` 35, `RandomToCache` 36 (`cache[v1] = 0 이상 v2 미만 난수`) |
| 합성 | `FocusSynthesisTarget` 33, `FocusSacrifice` 34 (v1 = 제물 번호), `DeleteSacrifice` 35, `DeleteAllSacrifices` 36 |

이펙트 서킷 종류와 실행 시점:

| 서킷 클래스 | 마스터(키) | 서브모듈 마스터(`master/…`) | 언제 도는가 |
|---|---|---|---|
| `SkillExecuteCircuit` | `skill_execute_effect_master` `(skill_code, execute_number)` | `skill_execute_submodule_master_v1` | 전투 `[107] 실행`에서 행동 1회마다(아이템 스킬 포함) |
| `SkillOrderCircuit` | `skill_order_effect_master` | `skill_order_submodule_master_v1` | 행동할 사람을 고를 때마다 생존 전원에 대해(OrderPoint가 가장 큰 1명이 행동) |
| `SkillAutoTargetCircuit` | `skill_auto_target_effect_master` | `skill_auto_target_submodule_master_v1` | 적의 메인 타겟 결정, 정한 메인 타겟이 쓰러졌을 때 |
| `AfterHitCircuit` | `after_hit_effect_master` `execute_number` | `after_hit_submodule_master_v1` | 스킬 실행 직후, 메인 타겟과 서브 타겟 각각 |
| `AfterAttackCircuit` | `after_attack_effect_master` | `after_attack_submodule_master_v1` | 스킬 실행→피격 후 다음, 시전자에게 1회 |
| `AfterBattleWinCircuit` | `after_battle_win_effect_master` | `after_battle_win_submodule_master_v1` | 승리 시 참가한 아군 유닛마다(그다음 아이템 드롭) |
| `AfterBattleLoseCircuit` | `after_battle_lose_effect_master` | `after_battle_lose_submodule_master_v1` | 패배 시 아군 유닛마다(던전 안 전투면 구금 처리 가능) |
| `AfterBattleRunCircuit` | `after_battle_run_effect_master` | `after_battle_run_submodule_master_v1` | 도망 시 아군 유닛마다 |
| `AfterLoadCharacterCircuit` | `after_load_character_flow_master` | `after_load_character_submodule_master_v1` | **세이브를 불러올 때** 보유 캐릭터마다 |
| `AfterLoadUniqueItemCircuit` | `after_load_unique_item_flow_master` | `after_load_unique_item_submodule_master_v1` | 세이브를 불러올 때 보유 유니크 아이템마다 |
| `CharacterAfterCreateCircuit` | `character_after_create_effect_master` `(character_code, execute_number)` | `character_after_create_submodule_master_v1` | `AddCharacter`(새 게임의 플레이어 포함)와 전투의 적 임시 캐릭터를 만들 때(세이브 로드 때는 안 돎) |
| `UniqueItemAfterCreateCircuit` | `unique_item_after_create_effect_master` `(item_code, execute_number)` | `unique_item_after_create_submodule_master_v1` | `AddUniqueItem`(적의 임시 장비 포함) |
| `XpCalculateCircuit` | `xp_calculate_effect_master` `(xp_type_code, execute_number)` | `xp_calculate_submodule_master_v1` | XP 줄을 표시할 때마다 |
| `SynthesisFlowCircuit` | `synthesis_flow_master` | `synthesis_submodule_master_v1` | 장비 강화 `[901] 합성` 실행 |
| `SmeltFlowCircuit` | `smelt_flow_master` | `smelt_submodule_master_v1` | 장비 강화 `[902] 제련` 실행 |

> 서킷마다 "처음부터 포커스된 것"이 다릅니다(예: 승리/패배/도망 후, 로드 후 캐릭터 서킷은 그 캐릭터가 처음부터 포커스, 생성 후 유니크 아이템 서킷은 그 아이템과 스킬이 포커스, 합성/제련은 타겟 아이템과 그 스킬이 포커스). 정확한 것은 README의 해당 마스터 문단과 각 `*Circuit.cs` 머리 주석을 보세요.

### 3.2 흐름 서킷 (자기 흐름 종류 enum을 가진 6종)
행 키는 `(소유자 코드, flow_number)`, 행마다 `flow_type`(이름)과 `param_1~3`. **점프는 모두 같은 소유자(서브모듈이면 같은 서브모듈)의 자신보다 큰 `flow_number`로만** 갑니다. 조건 점프의 `param_3`이 대상 번호입니다.

| 서킷 | 흐름 마스터 | 흐름 종류(번호) | 서브모듈 마스터 / `JumpToSubmodule` 번호 |
|---|---|---|---|
| `EventFlowCircuit` (이벤트) | `event_flow_master` | `None`0, `Finish`1, `Jump`2, `Adv`3, `Battle`4, `SetFlag`5, `ModFlag`6, `AddInventory`7, `JumpFlagEqual…LessOrEqual`8~13, `JumpInventoryEqual…LessOrEqual`14~19, `JumpCharacterHaveTag`20, `JumpUniqueItemHaveTag`21, `JumpRandom`22, `GameOver`23, `ReleaseCaptives`24, **`JumpToSubmodule`25** | `event_submodule_master_v1` / 25 |
| `EventFlowCircuit` (던전 이벤트) | `dungeon_event_flow_master` | 위와 같음(`ReleaseCaptives`는 던전 이벤트에서만 효과) | `dungeon_event_submodule_master_v1` / 25 |
| `AdvFlowCircuit` | `adv_flow_master` | `None`0, `Finish`1, `Jump`2, `TextMe`3, `Choice`4, `TextYou`5, `TextPlain`6, `SetFlag`7, `ModFlag`8, `AddInventory`9, `JumpFlag*`10~15, `JumpInventory*`16~21, `JumpCharacterHaveTag`22, `JumpUniqueItemHaveTag`23, `JumpRandom`24, `TextLog`25, **`JumpToSubmodule`26** | `adv_submodule_master_v1` / 26 |
| `CommunicationFlowCircuit` | `communication_{solo,duo,default}_flow_master` | `None`0, `Finish`1, `Jump`2, `Text`3, `Choice`4, `TextPlain`5, `SetFlag`6, `ModFlag`7, `AddInventory`8, `JumpFlag*`9~14, `JumpInventory*`15~20, `JumpCharacterHaveTag`21, `JumpUniqueItemHaveTag`22, `JumpRandom`23, `TextLog`24, **`JumpToSubmodule`25** | `communication_submodule_master_v1`(세 마스터가 **하나를 공유**) / 25 |
| `FacilityFlowCircuit` | `facility_flow_master` | `None`0, `Finish`1, `Jump`2, `SetFlag`3, `ModFlag`4, `AddInventory`5, `JumpFlag*`6~11, `JumpInventory*`12~17, `JumpCharacterHaveTag`18, `JumpUniqueItemHaveTag`19, `JumpRandom`20, `JumpOperatorExists`21, `JumpOperatorNotExists`22, `JumpOperatorHaveTag`23, **`JumpToSubmodule`24** | `facility_submodule_master_v1` / 24 |
| `DefenseFlowCircuit` | `operator_slot_defense_flow_master` | `None`0, `Finish`1, `Jump`2, `AddDefense`3 (v1 고정값), `AddDefenseByStatus`4 (v1 스테이터스 × v2 %, 소수 버림), `JumpFlag*`5~10, `JumpOperatorHaveTag`11, `JumpRandom`12, **`JumpToSubmodule`13** | `operator_slot_defense_submodule_master_v1` / 13 |

이벤트의 `Battle`(`param_1` = 전투, `param_2` = 결과를 담을 플래그: 승리 1, 도망 2, 패배 3) 칸의 전투에는 **플레이어(캐릭터 1)가 있는 파티**가 고정으로 나갑니다. 게임 오버는 `GameOver` 칸 또는 던전 전투 패배(주인공이 든 파티) 때 납니다. 점프 조건 비교(`FlagJump`/`InventoryJump`/`HaveTagJump`/`RandomJump`)만 서킷들이 코드를 나눠 씁니다. 자세한 인자와 검증은 README의 이벤트/ADV/교류/시설/방어력 문단.

### 3.3 흐름의 부수 효과 기본 원칙
- `SetFlag`/`ModFlag`/`AddInventory` 칸은 **화면에 나오지 않고** 멈추지 않으며 실패해도 알리지 않습니다(가능한 만큼만). `AddInventory` 개수는 음수면 뺌, 유니크 아이템과 캐릭터는 `1`/`-1`만 허용(로드 검증).
- 난수는 항상 `GameSession.NextRandom`(기본 `Random.Shared`, 세이브에 저장되지 않으며 테스트가 바꿔 끼움)을 통합니다.

### 3.4 서브모듈 (함수처럼 공통 동작 재사용)

`JumpToSubmodule`은 **모든 이펙트/흐름 종류 공통의 개념**입니다(번호는 종류마다 §3.1·§3.2 표). 규칙:

1. 서브모듈은 **서킷 종류마다 따로** 정의합니다(전투 승리 후용 서브모듈을 이벤트에서 부를 수 없음). 파일: §3.1·§3.2 표의 서브모듈 마스터.
2. 열: 이펙트 계열 `submodule_code`, `execute_number`, `effect_type`, `value_1`~`3`(키 `(submodule_code, execute_number)`). 흐름 계열은 그 흐름 마스터와 같은 열에서 첫 열만 `submodule_code`(예: 이벤트 `submodule_code, flow_number, flow_type, param_1..3`; ADV는 뒤에 `text`, `force_name`; 교류는 뒤에 `text`; 키 `(submodule_code, flow_number)`). 서브모듈 코드는 그 **종류 안에서 공유**됩니다(스킬별/캐릭터별/아이템별 이펙트 마스터가 있는 종류도 서브모듈은 하나를 같이 씀).
3. **호출한 서킷의 상태를 그대로 씁니다**: 캐시, 포커스, 시전자/타겟, 스테이터스 작업 사본(흐름 계열은 플래그, 인벤토리, 오퍼레이터, ADV 화자·선택지 등). 값은 **캐시로 주고받습니다**(부르기 전에 캐시에 넣은 값이 인자, 서브모듈이 캐시에 쓴 값이 결과). 끝나면 **호출 다음 이펙트로 돌아옵니다.** 서킷 고유 이펙트(`CaptureFocusedCharacter` 등)도 서브모듈 안에서 동작합니다.
4. **서브모듈 안에서 `JumpToSubmodule` 금지**(재귀 불가, 로드 오류). 서브모듈 안의 점프는 그 서브모듈의 더 큰 번호로만. 서브모듈 안의 `Finish`(또는 마지막 칸)는 **서브모듈만** 끝내고, 메인의 `Finish`는 서킷 전체를 끝냅니다.
5. 메인 마스터의 `JumpToSubmodule`이 가리키는 코드는 **같은 종류의 서브모듈 마스터에 있어야** 하고, 그 마스터가 없거나 코드가 없으면 로드 오류. 서브모듈을 넘기지 않은 실행기에서는 호출 칸이 아무 일도 하지 않습니다.
6. 이벤트·ADV·교류는 서브모듈 안에서 ADV/전투/대사/선택지를 만나 **멈춰도 다음 `Advance` 호출이 그 자리에서 이어 갑니다**(한 단계 호출 스택). 선택지의 점프 대상은 서브모듈 안이어야 합니다. 시설 서브모듈의 오퍼레이터 칸은 "어느 시설이든 가진 슬롯"이면 통과.

**캐시 칸 사용 약속(권장 — 엔진이 강제하지 않음):** 서브모듈은 호출자의 캐시를 **덮어쓸 수 있습니다.** 게임팩 안에서 칸을 나눠 약속해 두세요. 예: `200~219` = 인자(호출 전에 호출자가 씀), `220~239` = 결과(서브모듈이 씀), `0~199` = 호출자가 자유롭게 쓰는 지역값(서브모듈은 건드리지 않음), `240~255` = 서브모듈이 쓰는 임시값. 단, 일부 서킷은 **특정 캐시 칸을 결과로 읽습니다**: XP 계산 서킷은 **0번(값)·1번(최댓값)**, 자동 타겟/순서 서킷은 캐시가 아니라 `Result`(`SubTargetToResult`/`CacheToResult`)를 읽습니다. 캐시 인덱스는 0~255(`GamePack.MaxCacheIndex`)입니다.

---

## 4. 런타임 데이터 모델

### 4.1 게임팩 형식 (`.wra`)
zip(UTF-8 TSV). 항목: `pack.tsv` + `master/*_v1.tsv`. `GamePack.Load(Stream)`이 모든 마스터를 읽고 검증해 읽기 전용 `GamePack`(마스터 테이블, 조회 함수)을 만듭니다. 조회는 `GamePack.Find*`/`*Of` 계열(예: `FindCharacter`, `FindItem`, `FindSkill`, `SkillExecuteEffectsOf`, `FacilityFlowsOf`).

### 4.1a 모드 (중첩 게임팩, `pack_master`)
바깥 팩의 `master/pack_master_v1.tsv`(열 `order_number`, `pack_file`)가 zip 안의 중첩 `.wra`들을 `order_number` 순으로 얹고, 같은 마스터는 헤더 이름으로 열을 맞춰 합치며, **같은 키의 행은 나중 팩이 통째로 덮어씁니다**(바깥 팩 자신의 파일이 가장 나중). 로더보다 앞서 텍스트로 합쳐 메모리 zip을 만들고 기존 로더에 그대로 넘기므로 합친 결과 전체가 검증됩니다. `pack_master`가 없으면 아무것도 바뀌지 않습니다. 자세한 규칙·한계는 README의 **모드 (중첩 게임팩)**.
- **핵심:** `pack.tsv`는 바깥 팩의 것만 씀(id/title). 중첩 팩 안에 `pack_master` 금지(재귀 없음). 팩 최대 32개, 파일당 64MB. 행 삭제 기능 없음(덮어쓰기·추가만). 세이브는 바깥 팩 `id`에만 묶이고 모드 구성이 달라진 세이브의 호환은 지원하지 않음.
- **키 등록(중요):** 어떤 열이 키인지는 `src/Wera.Core/PackMasterKeys.cs`의 `PackMasterKeys.All`에 마스터 경로별로 등록합니다. **새 마스터를 만들면 반드시 여기에 등록**하세요(테스트가 모든 `...MasterPath` 상수와 샘플 마스터의 키 열을 검사해 빠뜨리면 실패). 로더가 따로 유일성을 요구하는 두 번째 키는 `KeyedAlt`로(예: `image_master`의 `(kind, target_code, variation_number)`), 열 순서가 정해진 마스터는 `ColumnOrder`로(`dungeon_master`), 키 없이 이어 붙일 옛 마스터는 `AppendOnly`로 등록합니다.
- **만들기·진단:** `python tools/zip-wrapper-pack.py <폴더> <출력.wra>`로 묶고, 읽은 뒤 `GamePack.Layers`(`Name`, `OrderNumber`, `Id`, `Title`)로 적용된 레이어를 봅니다. 구현은 `GamePack.Layers.cs`(`ResolveLayers`, `Merge`, `MergeTsv`), 테스트는 `PackLayerTests.cs`.

### 4.2 `GameSession` (게임 중 상태, `src/Wera.Core/GameSession*.cs`)
`GameSession.NewGame(pack)`이 새 게임을, `GameSession.FromSaveJson(pack, json)` / `ToSaveJson()`이 세이브를 다룹니다. 주요 상태:

| 상태 | 내용 |
|---|---|
| `Characters` (`CharacterCollection : Dictionary<string, CharacterInstance>`) | **보유 캐릭터. 키 = 인스턴스 ID(ULID 문자열)**. `CharacterInstance { InstanceId, CharacterCode, OrderNumber, Statuses(코드→값), SkillPool, SkillSlots, ExploreTurn, DefenseDayCount … }` |
| 같은 캐릭터 여럿 | `carry_limit`까지 같은 코드의 인스턴스를 여럿 가질 수 있음(플레이어는 늘 1). 보유 캐릭터를 가리키는 **모든 것**(파티 유닛, 오퍼레이터, 방어 파티, 파트너/어시스트, 상태이상, 태그, 장비의 캐릭터 참조, 탐사·방어 기록)은 **인스턴스 ID**. 마스터가 정한 곳(흐름 조건, 교환, 태그 조건, 인벤토리 수)에서 "코드가 X인 캐릭터"는 **어느 인스턴스든**, 하나를 골라야 하면 **첫 인스턴스**(`OrderNumber` → `InstanceId`). 한 복사본은 파티 슬롯·오퍼레이터 슬롯·방어 파티 중 **한 곳에만** 있을 수 있음. 도우미: `CharacterInstancesOf`, `FirstInstanceOf`, `InstanceIdOf`, `OwnedCountOf`, `CharacterLimitOf`, `CanAddCharacter`, `AddCharacter` |
| 파티 | 16개(`PartyCount = 16`, `파티-A`~`P`), `Parties`와 `CurrentParty`. `PartyInstance { Name, Units[9] }`(칸 0~2 전위, 3~5 중위, 6~8 후위), `PartyUnit { CharacterCode, Equipment(장비슬롯→EquipRef), IsLeader, InstanceId }` (유닛 = 캐릭터 + 장비). 파티끼리는 캐릭터·장비를 공유할 수 없음. 파티에는 리더(리더 태그 보유자)가 한 명 이상 있어야 던전·전투에 파견 가능 |
| 장비 | `EquipRef { Kind: UniqueItem/CommonItem/Character, Code 또는 Instance, … }`. 끼울 수 있는 것은 유니크 아이템·커먼 아이템·**캐릭터** 모두(슬롯 태그 규칙으로 정함). 유닛이 파티에서 빠지면 장비는 해제되어 보유로 돌아옴 |
| 인벤토리 | `InventoryItems`(커먼 아이템 코드 → `{Amount}`, 번치는 부품으로 풀려 들어감), `UniqueItemInstances`(인스턴스 ID(ULID) → `UniqueItemInstance { InstanceId, ItemCode, OrderNumber, Tags, Statuses, Skill, SkillPool, Mods }`) |
| 태그 | 캐릭터·유니크 아이템 인스턴스가 **각자의 태그**를 가짐(마스터에서 복사, 게임 중 추가/제거 가능). 커먼 아이템은 늘 마스터 태그 |
| `PlayerFlags` | 플래그 코드 → `long`. 던전·상점·교환·이벤트·시설 조건과 흐름의 `SetFlag`/`ModFlag`/`JumpFlag*`가 씀(화면에 표시되지 않음). 방어전 위협도도 플래그 하나 |
| 턴 | `TurnCount`, 현재 날짜/시간대는 `turn_master`에서. `AdvanceTurn`은 다음으로 큰 `turn_count`로, 마지막이면 그대로 |
| 아지트 | `hideoutParties`(층별 방어 파티 = "보스"), `hideoutFacilities`(층별 지은 시설), `hideoutOperators`(`(층 생긴 순서, 지은 시설 여부, 슬롯 코드)` → 인스턴스 ID). 층 목록은 **새 층이 앞(맨 위, index 0)에 들어가고 기존 층은 밀림**; 가장 처음 있던 층이 마지막 index = 최종층 |
| 구금 | `DungeonCaptives`(던전 코드 → 갇힌 `PartyUnit`들). 구금된 적/아군은 태그로 표시 |
| 파트너/어시스트 | `PartnerId`/`AssistId`(인스턴스 ID), 교류 상대와 홈 화면 슬롯(아직 다른 쓰임은 없음) |
| 화면 | `Screen`(§4.4), 전투 상태(`EnemyInstances`: 적 슬롯 → 임시 `CharacterInstance`, 행동 설정 등)는 **세이브에 들어가지 않음** |

### 4.3 파티·전투 개요
- 전투는 **이벤트 흐름의 `Battle` 칸**, 던전 타일 `FixedBattle`, 던전 인카운트(`encounter_rate` + `dungeon_encounter_master`)로 시작합니다. 적은 `battle_enemy_master`의 프리셋 파티 중 가중치로 하나를 뽑아 **임시 캐릭터 인스턴스**로 만듭니다(캐릭터 생성 후 서킷이 돌아 스킬도 정함).
- 화면은 적/아군 3×3 슬롯과 커맨드(`[101] 다음 캐릭터`, `[102] 행동 설정`, `[103] 아이템`, `[104] 이전 캐릭터`, `[105] 이동`, `[106] 도망`, `[107] 실행`). `[107] 실행`은 모두 행동을 정해야 활성. 한 번에 한 명씩 OrderPoint가 큰 순으로 행동 → `SkillExecuteCircuit` → `AfterHitCircuit`(메인, 서브마다) → `AfterAttackCircuit`. 전원이 1회씩 행동하면 1턴 종료(진형 당기기: 전열이 전멸하면 중열→전열, 후열→중열).
- 종료: 승리(아군 유닛마다 `AfterBattleWinCircuit` → 드롭), 패배(`AfterBattleLoseCircuit`), 도망(`AfterBattleRunCircuit`).
- 참고: 코드에 `ActionOrder.Sort`가 있으나 **전투에서 쓰이지 않습니다**(순서는 `SkillOrderCircuit`의 OrderPoint로 직접 고름).

### 4.4 화면과 입력 코드 규약
화면은 `Screen` enum으로 정해지고 모든 클릭 가능한 글자는 `[코드]`를 입력해도 동작합니다. 코드 규약:

| 코드 | 의미 |
|---|---|
| `101`~`108` (홈 RPG) | `[101]` 던전, `[102]` 아지트, `[103]` 이벤트, `[104]` 상점, `[105]` 파티 설정, `[106]` 장비 강화, `[107]` 교류, `[108]` 턴 종료 |
| `200`/`201`/`202`/`203` | 세이브/동료 확인/장비 확인/아이템 확인 |
| `300`, `400`(`401`~`404`) | 로드, 디버그(캐릭터/장비/아이템/스킬 추가) |
| `901`/`902`/`903` | 장비 강화의 합성/제련/개조(그 화면 안에서 실행/취소와 같은 번호 재사용) |
| `998`, `999` | 개조 슬롯 목록의 빼기, **돌아가기**(코드 입력줄 바로 위 오른쪽 끝에 늘 같은 자리) |
| `0` | 비우기/해제/파티 이름·스킬 풀 진입 등 맥락별 |
| `1000`/`1001` | 아지트·전투 화면의 위/아래 스크롤. `1002` = 아지트의 **시설 확인** |
| `101`~`106` (목록 화면) | 목록 페이지 버튼(첫 페이지, −2, −1, +1, +2, 마지막) — 목록 줄 번호는 페이지마다 1부터라 `ListPageSize ≤ 100` |

`Screen` 값: `Home`, `DungeonSelect`, `DungeonPartySelect`, `DungeonMove`, `GameOver`, `ShopList`, `PartySetup`, `PartyRename`, `EquipmentSetup`, `LeaderSetup`, `SkillSetup`, `SkillCharacter`, `SkillSlotSelect`, `SkillPoolList`, `Battle`, `UnitStatus`, `EquipmentList`, `EquipmentSelect`, `CharacterSelect`, **`Defense`**, `Hideout`, **`FacilityCheck`**, `HideoutExpand`, `HideoutFloorDetail`, `HideoutFacilityDetail`, `HideoutFacilitySelect`, `HideoutBuild`, `HideoutOperatorSelect`, `EquipmentEnhance`, `Event`, `EventPlay`, `Interaction`, `CommunicationPlay`, `Debug`, `DebugAddCharacter`, `DebugAddEquipment`, `DebugAddItem`, `DebugSkillCharacter`, `DebugAddSkill`, `StatusCheck`, `CharacterDetail`, `EquipmentDetail`, `ItemDetail`, `SkillDetail`, `CompanionCheck`, `EquipmentCheck`, `ItemCheck`, `SupportSelect`, `ShopTrades`, `Load`. UI 쪽 렌더링은 `src/Wera.Web/Pages/Home.razor` 한 파일에 거의 다 있습니다(게임 화면은 960×900 고정, 검은 바탕·흰 글자·등폭 폰트).

던전 이동: 코드 입력칸에 `W A S D`(대소문자 무시, 한글 `ㅈ ㅁ ㄴ ㅇ` 입력 상태·방향키도 동작) 즉시 이동, `Enter`는 지금 칸 상호작용(출구 `Exit`, `Interact`가 겹친 전투/ADV/워프/던전 이벤트). **던전에서 나가는 곳은 `Exit` 칸뿐**입니다. 새 게임과 불러오기는 항상 홈으로 들어갑니다(세이브에 저장된 화면은 무시).

### 4.5 세이브 (`.wsv`, JSON)
`SaveData`(`GameSession.cs`) 필드(앞에서부터, 위치 인자): `Version`, `PackId`, `Screen`, `PlayerFlags`, `InventoryItems`, `Characters`, `Party`(예전), `StatusEffects`, `UniqueItems`, `CharacterTags`, `EquippedItems`(예전), `Partner`, `Assist`(예전), `Turn`, `PartyInstance`(예전), `Parties`, `CurrentParty`, `HideoutParties`, `HideoutFacilities`, `HideoutOperators`(`{FloorOrder, Built, SlotCode, CharacterCode, InstanceId}`), `PartnerId`, `AssistId`, `DungeonCaptives`. **새 필드는 항상 맨 뒤에 추가**하고 null이면 JSON에서 생략됩니다.

- **`SaveVersion = 1`**: 릴리스 전이라 **올리지 않습니다**(사용자 지시: 지시 전까지 유지). 기존 세이브와의 호환·마이그레이션은 **신경 쓰지 않습니다**(예전 필드를 읽는 코드는 남아 있지만 더 늘리지 않음).
- 불러올 때 **검증**: 캐릭터/스킬/상태이상/장비/태그 코드가 마스터에 있는지, `0 ≤ HP ≤ max_hp`, 같은 인스턴스 ID 중복·ULID 형식, 한 캐릭터 복사본이 두 곳에 있는지, `carry_limit` 초과, 슬롯 태그 규칙 부합, 구금된 적이 파티·아지트에 있는지, 구금된 아군은 던전 마스터에 있고 태그가 있는지 등. 어기면 `SaveFormatException`.
- 세이브에 **저장되지 않는 것**: 진행 중인 이벤트·ADV·전투·방어전 화면 상태, `NextRandom`, `HideoutInvaded`(턴 종료 안에서 쓰이고 사라짐). 세이브 파일에는 `Screen`이 적히지만 불러올 땐 무시합니다.
- 로드 후 서킷(`after_load_*_flow_master`)이 세이브를 불러올 때 캐릭터·유니크 아이템 인스턴스에 실행됩니다.
- 코드 키(`"1001"`)는 정수를 문자열로 적은 것이고 숫자가 아니면 세이브 오류.

### 4.6 턴 종료 → 방어전 → 시설 생산
홈 `[108] 턴 종료`(`EntryAction.EndTurn`):
1. `AdvanceTurn()`(마지막 턴이면 회색이라 안 일어남).
2. **침입 판정** `RollDefenseEnemy()`: 게임 설정 `ThreatFlagCode`의 플래그 = **위협도**(이벤트/ADV의 `SetFlag`/`ModFlag`로 올림). `hideout_defense_enemy_master`를 `priority`(작을수록 먼저) 순으로, 위협도 ≥ `min_threat`인 행만 `chance`(%)로 굴려 처음 통과한 행이 등장하고 위협도가 `consume_threat`만큼 줄어듭니다(0 아래로 안 감). 통과한 행이 없으면 평화로운 날. **프리셋 파티가 보유 한도(`carry_limit`)를 넘기게 만드는 캐릭터를 포함하면 침입 자체가 실패**(위협도 소모 없음).
3. 침입이 나오면 `Screen.Defense`: 방어전 **결과는 화면에 들어올 때 이미 계산이 끝나 있고** 화면은 로그를 **Enter(코드 `1`, 아무 키나 클릭)마다 한 줄씩** 펼칩니다(세이브 불가). 마지막 Enter에서 홈으로 돌아가 요약 안내가 나옵니다. 화면 없이 로그만 얻으려면 `GameSession.RunDefense()`(상태 변경은 똑같이 적용).
4. 방어전이 끝난 뒤(또는 침입이 없으면 바로) **시설 서킷** `RunFacilityCircuits()`: 층을 위(0번)부터 차례로, 층마다 기본 시설 → 지은 시설의 `facility_flow_master`를 실행해 생산(`AddInventory`). `HideoutInvaded`(최종층까지 뚫림)면 **그 턴은 생산하지 않습니다.** 안내는 "시설 생산: 철광석 +3" 형태.

**방어전 규칙**(`GameSession.Defense.cs`):
- 적 파티는 프리셋 파티로 만든 **임시 인스턴스**(체력은 층을 지나도 이어짐). 처리는 (파티, 층) 대기열을 앞에서부터. **시작 = 맨 위(가장 최근 확장한) 층 → 아래로, 가장 처음 있던 층이 최종층.**
- 한 층의 순서: ① **기본 시설** ② **지은 시설** — 시설마다 오퍼레이터가 슬롯 순서대로 **한 명씩 한 번** 부딪치고(피해 = 0 ~ 그 슬롯 방어력 서킷의 결과 사이 무작위 정수, 쓰러지지 않은 적들에게 무작위로 나눠 배분, 체력을 넘는 몫은 다른 적에게 재배분: `DefenseDamage.Roll/Distribute`), 그다음 **시설이 한 번**(`facility_defense_master`의 값) 부딪침 ③ **보스** = 그 층의 방어 파티와 **자동 전투**(양쪽 모두 장착한 스킬 중 무작위, 대상 자동, 승패 뒤 서킷은 돌지만 드롭 없음). `DefenseBossTurnLimit = 50`턴 안에 안 끝나면 보스가 층을 내줌, 싸울 수 있는 방어 파티원이 없으면 그냥 지나감.
- 적 파티 전원의 체력이 0이 되면 그 자리에서 **쓰러져 구금**(`AddCapturedEnemy`, 한도가 차면 사라짐). 파티가 층을 **지나갈 때마다 추가 침입 판정을 다시** 굴려 통과하면 새 파티가 맨 위 층에서 대기열 뒤에 섬.
- 최종층을 지나가면 `HideoutInvaded` → 이번 시설 생산 건너뜀.
- 싸운 캐릭터(부딪친 오퍼레이터, 보스와 싸운 방어 파티원)는 `RecordDefense`로 기록되어 **다음 날(`DefenseDayCount + 1`)** 행동할 수 없습니다(`CanExplore`가 참가 불가로 판정; 이벤트·던전·교류 항목이 회색). `ExploreTurn`: 한 턴에 한 번만 탐사.

**시설 확인**(`[1002]`, `Screen.FacilityCheck`, 읽기 전용): 층 순서대로 기본/지은 시설의 오퍼레이터(선 캐릭터와 슬롯 최대 방어력), 시설 방어력, **정적인** 생산 설명(시설 흐름과 서브모듈의 `AddInventory`를 "이름 +n"으로, 조건·확률 점프가 있으면 "(조건·확률에 따라 달라질 수 있음)", 오퍼레이터를 보는 점프가 있으면 "(오퍼레이터에 따라 달라질 수 있음)"; 난수 실행 없음).

### 4.7 구금 (captive) 체계
- **구금된 적**: 게임 설정 `CapturedEnemyTagCode`의 태그를 가진 보유 캐릭터. 방어전에서 진 적이 `AddCapturedEnemy`로 한도 안에서 들어옵니다. 보유 목록·캐릭터 확인·**교류(파트너/어시스트)** 에는 나오지만 **파티 편성·아지트(방어 파티, 오퍼레이터)·던전·이벤트·전투에는 쓸 수 없고**, 구금된 적이 파티나 아지트에 있는 세이브는 거절합니다.
- **구금된 아군**: 던전 안 전투(이벤트의 전투는 제외)에서 **졌을 때**, 전투 패배 후 서킷이 파티원마다 돌며 `CaptureFocusedCharacter`(포커스한 캐릭터를 구금)를 만나면 전투 화면을 떠날 때 그 캐릭터가 **파티에서 빠져** 구금된 아군 태그(`CapturedAllyTagCode`)를 얻고 **그 던전에 갇힙니다**(`DungeonCaptives`; 장비를 든 채로). `JumpNotInDungeon`(v1 = 점프할 번호; 던전 밖이면 점프)으로 조건을 가립니다. 구금된 아군은 파티 편성·캐릭터 확인 등 **어디에도 나오지 않고** 그가 낀 유니크 아이템도 나오지 않습니다. **플레이어(캐릭터 1)는 구금되지 않습니다.**
- **구출**: 던전 이벤트 흐름의 **`ReleaseCaptives`**(번호 24)가 그 던전에 갇힌 구금된 아군을 **전부** 구출합니다(태그가 떨어지고 장비와 함께 보유 캐릭터로 돌아오되 **파티에는 돌아오지 않음** — 따로 배치해야 함). **"던전 클리어" 자체는 엔진 개념이 아니라 플래그**(`SetFlag`)로 게임팩이 다룹니다. 코드: `GameSession.RescueCaptives(던전 코드)`.

### 4.8 게임 설정 (`GameSettingType`)

| 이름(번호) | 값 종류 | 기본/규칙 |
|---|---|---|
| `AttributeAdvantageMultiplier` (1) | Decimal | 1.5. **읽는 곳(전투 계산)이 아직 없음** |
| `AttributeDisadvantageMultiplier` (2) | Decimal | 0.5. 위와 같음 |
| `ListPageSize` (3) | Long | 20, 1 이상 100 이하(목록 한 페이지 항목 수) |
| `SynthesisSacrificeCount` (4) | Long | 1, 1 이상(합성에 바칠 같은 코드의 다른 유니크 아이템 수) |
| `EnhanceLevelMax` (5) | Long | 0 이상. 행이 없으면 강화 상한 없음 |
| `SmeltStoneItemCode` (6) | Long | 1 이상, 커먼 아이템 코드여야 함. 행이 없으면 제련 불가 |
| `ThreatFlagCode` (7) | Long | 1 이상, 위협도를 담는 플레이어 플래그 코드. 방어전 적 마스터에 필요 |
| `PlayerTagCode` (8) / `LeaderTagCode` (9) / `MainCurrencyTagCode` (10) | Long | 1 이상, 행이 없으면 1 / 2 / 3, 태그 마스터에 있어야 함 |
| `CapturedEnemyTagCode` (11) / `CapturedAllyTagCode` (12) | Long | 1 이상, 행이 없으면 그 기능 없음, 태그 마스터에 있어야 함 |
| `None` (0) | — | 자리 표시 |

다섯 태그 코드는 서로 달라야 하고 값 종류·최솟값을 어기면 로드 오류입니다.

### 4.9 스테이터스 예약 코드, 경험치/레벨업, 속성
- **예약 스테이터스 코드**: `1 level`, `2 hp`, `3 xpp`, `4 max_hp`, `5 xpp_max`, `6 exp`, `7 exp_max`(이름은 자유), 유니크 아이템 `8 enhance_count`. **9번 이상은 게임팩 마음대로.** `level`과 `hp`(및 그 레벨 0 행)만 필수입니다. 새 캐릭터는 레벨 0 행의 `initial_value`로 채워지고 `max_hp`/`xpp_max`는 없으면 각각 hp/xpp 행의 `max_value`로 폴백합니다.
- **경험치/레벨업은 엔진이 하지 않습니다.** 코드에서 `exp`/`exp_max`는 **캐릭터 상세의 Exp 바 표시**와 `0 ≤ exp ≤ exp_max` 범위 보정에만 쓰이고, 레벨이 오르거나 스펙이 자라는 로직은 **엔진에 없습니다.** 게임팩이 `after_battle_win_effect_master`(승리 후 서킷: 참가한 아군 유닛마다, 그 캐릭터가 포커스된 채 시작)에서 `exp` 증가, 레벨 비교, 스테이터스 성장을 이펙트(`CacheAdd`, `CacheToFocusedCharacterModValue`, `JumpCache*` 등)로 직접 구현해야 합니다. 상세 규칙은 README의 전투 승리 후 이펙트 문단.
- **속성**: `character_inventory_master`의 유리/불리속성 코드와 `attribute_master`는 있고 상성 규칙은 §1.5에 결정만 적혀 있지만 **전투 계산에는 아직 적용되지 않습니다**(설정값만 읽을 수 있음: `GamePack.AttributeAdvantageMultiplier`/`AttributeDisadvantageMultiplier`).

### 4.10 기타 알아둘 것
- **홈 화면 위쪽 슬롯**: 당신(플레이어, 불변), `[1]` 파트너, `[2]` 어시스트(교류의 상대; 파티와 독립). 참가 불가 캐릭터는 진한 녹색(`#2f8f5b`)으로 보이는 알림이 있음.
- **상세 화면**과 목록 표(열 이름이 있는 표, 20개씩 페이지), BBCode 일부(`[b]`, `[color=…]`, 이스케이프 `\[`, `\]`, `\\`), ADV 대사 상자 크기 검증(한 줄 83칸 × 3줄, `TextPlain` 94칸 × 3줄)은 README의 해당 문단.
- **임베디드 이펙트**(상태이상 이펙트가 가리키는 하드코딩 클래스)는 `EmbeddedEffectRegistry.Default`에 등록하고 지금은 `1 = HealEffect`만 있습니다. 새 임베디드 이펙트는 코드를 써야 하는 엔진 작업입니다.
- 장비 강화 화면: **합성**(같은 코드의 다른 장비를 제물로 `SynthesisSacrificeCount`개), **제련**(제련석 소모, 확률은 서킷), **개조**(개조 슬롯에 커먼 아이템을 끼워 스테이터스/스킬 부여) 세 모드가 있고 모두 서킷/마스터로 정의됩니다. 강화 횟수는 유니크 아이템 스테이터스 8번.

---

## 5. 알려진 공백 / 엔진 밖에서 할 일

**방침:** 엔진 기능은 **대부분 완성**으로 봅니다. 새 기능을 먼저 제안하기보다 **게임팩을 만들면서 발견되는 엔진 버그·수정사항을 엔진에서 고쳐 가며** 게임팩을 완성합니다. **게임팩이 완성되면 그때 V1로 확정**합니다(그전에는 `SaveVersion` 유지, `main` 머지 보류).

| 항목 | 현재 상태(코드 확인) |
|---|---|
| **턴 종료 캐릭터 서킷**(개별 캐릭터 서킷이 정의돼 있으면 그것, 없으면 범용; 식량 소모, 구금된 아군이 확률로 적이 되거나 사망·삭제) | **엔진에 없음.** 사용자가 **엔진 작업이 아니라고 판단**(게임팩 쪽 몫). `AfterTurnEnd`는 상태이상 트리거 이름일 뿐 캐릭터 서킷이 아님 |
| **구금된 적의 설득·석방** | **엔진에 없음.** 엔진 작업이 아니라고 판단. 구금된 적은 교류(파트너/어시스트)에서만 쓸 수 있으니, 게임팩이 교류 흐름에서 `AddInventory`/태그 점프 등으로 연출할 수 있는지 먼저 보고 모자라면 엔진에 요청 |
| **속성 상성 배율** | 설정값은 있으나 **전투 계산에 미적용**(`AttributeAdvantageMultiplier`/`DisadvantageMultiplier`를 읽는 전투 코드가 없음). 결정된 규칙은 §1.5 |
| **경험치/레벨업** | 엔진에 **없음**(§4.9). 승리 후 서킷에서 게임팩이 구현 |
| 전투의 **적 포획**(일반 전투에서 잡아 보유) | 없음(방어전 패배한 적의 구금만 있음). README의 "적을 포획하면 …옮길 예정" 문장은 미구현 |
| 던전 클리어 | 엔진 개념 없음(플래그로 다룸). `ReleaseCaptives`만 구출을 담당 |
| 이미지 | 구현됨: 2.7a 참조 |
| 진행 중 상태 저장 | 전투/이벤트/ADV/방어전 도중에는 저장 불가(세이브에 안 담김) |
| `ActionOrder` | 클래스가 있으나 전투에서 쓰이지 않는 잔재(동순위 규칙이 TODO였음) |
| 임베디드 이펙트 | `HealEffect`뿐 |

---

## 6. 게임팩 제작 빠른 시작

### 6.1 부팅에 필요한 최소 마스터
`GamePack.RequiredMasterPaths`의 **10개 + `pack.tsv`**. 테스트의 `CoreTests.PackZip`이 빠진 것을 이 기본값으로 채웁니다(그대로 쓰면 로드됩니다):

```text
pack.tsv                         id<TAB>demo / title<TAB>데모          (key/value 행)
master/attribute_master_v1.tsv   attribute_code, attribute_name         1 불 / 2 물
master/tag_master_v1.tsv         tag_code, tag_name                     1 플레이어 / 2 리더 / 3 메인 통화
master/status_master_v1.tsv      status_code, status_name               1 level / 2 hp (3 xp)
master/character_inventory_master_v1.tsv
                                 inventory_code, character_name, carry_limit, advantage_attribute_code, disadvantage_attribute_code
                                 1 당신 1 1 2                           (캐릭터 1 = 플레이어, 필수)
master/character_tag_master_v1.tsv   character_code, tag_code           1 1 / 1 2   (플레이어 태그 + 리더 태그)
master/character_status_master_v1.tsv
                                 character_code, status_code, level, initial_value, min_value, max_value
                                 모든 캐릭터에 level(1)·hp(2)의 level 0 행  (예: 1 1 0 1 1 99 / 1 2 0 1 0 1)
master/time_slot_master_v1.tsv   code, name                              1 아침
master/turn_master_v1.tsv        turn_count, year, month, day, day_count, time_slot_code   1 1 1 1 1 1
master/common_item_inventory_master_v1.tsv   inventory_code, item_name, carry_limit    2 금화 99999   (아이템 2 = 메인 통화)
master/common_item_tag_master_v1.tsv         item_code, tag_code        2 3   (메인 통화 태그)
```

여기서 출발해 필요한 마스터를 하나씩 더하세요. **선택 마스터를 넣으면 그것이 가리키는 상위 마스터도 필요**합니다(예: `communication_master`는 `communication_group_master`, `facility_*`는 `facility_master`).

### 6.2 샘플 게임팩이 보여 주는 것 (`samples/sample-pack`, 76개 마스터)

| 영역 | 샘플의 예시 |
|---|---|
| 설정 | `game_setting_master`: 설정 12종 전부(위협도 플래그 100, 제련석 5, 강화 상한 20, 예약 태그 1/2/3, 구금 태그 10/11) |
| 캐릭터 | 플레이어 1 "당신", 용사 7001, 마법사 7002, 전사 7003, 도적 7004. 스테이터스 코드 3~8 예약 + 20·21(예시), 30 STR/31 CON/32 INT(상세 표시 순서) |
| 던전 | 던전 1001 초원, 1002 동굴, **1999 테스트 던전**(12×7 맵 + 계단으로 오가는 작은 맵, 모든 타일 종류, 상자 `C`=`Interact`+`DungeonEvent` 6001, 인카운트 10 % 전투 5001) |
| 상점·교환 | 상점 2001, 2002, 2999(커먼/유니크/캐릭터 교환 포함, 조건 없이 새 게임부터 열림) |
| 이벤트/ADV | 이벤트 3001(테스트), 3002(전투 5001 결과 플래그 2001 → 승리 ADV 4002, 패배 `GameOver`), 전투 5001(도망 불가) |
| 스킬 | 베기 1001, 불꽃 1002(`SameRow`), 치유 1003, 집중 1004, 부활 1005(`AllyFallen`); 자동 타겟(hp 낮은 쪽) 예, 순서 서킷 예; 모든 캐릭터가 스킬 1001~1004를 슬롯 1~4에 장착(생성 후 서킷) |
| 장비 | 유니크 아이템 철검 8001 등, 슬롯 태그/부품 슬롯/개조(`mod_slot`, 개조 스테이터스·스킬), 강화 스테이터스(레벨 0~20), 합성·제련 흐름(제련: 10% 대실패/60% 실패/30% 성공), 디버그 시작 아이템 8007 |
| 교류 | 그룹 1 "대화", 교류 1 "인사하기", 솔로/듀오/기본 흐름 |
| 아지트 | 시설 1 정문(기본 시설)/2 대장간/3 마법탑(플래그 조건), 오퍼레이터 슬롯 1 정비(리더 태그만)/2 경비, 방어력 흐름, 시설 방어력, 방어전 적 2종(위협도 10 이상 50 % / 30 이상 30 %), 아지트 확장·시설 재료 |
| 서브모듈·서킷 | **샘플에는 서브모듈 예시가 없고**, 승리/패배/도망/피격/공격 후·XP 종류·로드 후 서킷도 샘플에 마스터가 없음(코드와 테스트에서만 검증) |

### 6.3 흔한 로드 오류 점검표

- [ ] `pack.tsv`에 `id`, `title`이 있는가. zip 항목 이름이 `pack.tsv`, `master/…_v1.tsv`(최상위 폴더 없이)인가.
- [ ] 필수 10개 마스터가 모두 있는가("…가 반드시 필요합니다").
- [ ] 헤더가 **정해진 열 이름 순서**로 시작하는가("헤더가 '…'이어야 합니다"). `memo` 열은 어디에 있어도 됨.
- [ ] 코드가 숫자인가, `flow_type`/`effect_type`/`tile_effect`/`condition_type` 등이 **이름**(숫자 아님)인가, 오타가 없는가.
- [ ] 예약 항목: 캐릭터 1(플레이어)과 플레이어 태그, 태그 1/2/3(또는 게임 설정이 바꾼 코드), 스테이터스 level(1)·hp(2), 커먼 아이템 2(메인 통화)와 메인 통화 태그, **모든 캐릭터의 level·hp `level 0` 행**.
- [ ] 인벤토리 코드(커먼/유니크/번치/캐릭터)가 **서로 겹치지 않는가**(1은 플레이어 전용).
- [ ] 외래키: 가리키는 코드가 해당 마스터에 있는가(태그, 스테이터스, 스킬, 전투, ADV, 프리셋, 시설, 슬롯…).
- [ ] 같은 키를 두 번 쓰지 않았는가(복합키 포함: 조건표는 `(코드, group_number, flag_code)`).
- [ ] `min_value ≤ initial_value ≤ max_value`, `level ≥ 0`, `carry_limit ≥ 1`, `amount ≥ 1`.
- [ ] **점프**가 같은 소유자(서브모듈이면 같은 서브모듈)의 **자신보다 큰** 번호를 가리키는가. `JumpRandom`의 모수 ≥ 1. `Choice` 행들이 연속인가.
- [ ] `JumpToSubmodule`이 같은 종류의 서브모듈 마스터에 있는 코드를 가리키는가. 서브모듈 안에서 다시 부르지 않았는가.
- [ ] 이펙트 인자: `status_code`는 스테이터스 마스터에, `cache_index` 0~255, `SetCacheValue` 소수 자릿수 0~28, 장비/스킬 슬롯 코드는 해당 슬롯 마스터에.
- [ ] `ADV`/교류 `text`의 BBCode가 올바른가, 대사 상자 크기(83칸×3줄 등)를 넘지 않는가.
- [ ] 던전: 맵이 있으면 도입 지점(`entry_*` 셋 모두), 도입 지점·`Warp` 도착 칸이 맵의 글자 칸인가, `pos_y` < 30, `dungeon_tile`의 글자가 맵에서 쓰이는 글자와 일치하는가.
- [ ] `EnhanceLevelMax`를 정했다면 강화 스테이터스 마스터에 레벨 1~상한이 모두 있는가. `SmeltStoneItemCode`가 커먼 아이템인가. `ListPageSize` ≤ 100.
- [ ] 방어전: `hideout_defense_enemy_master`에 `ThreatFlagCode` 설정과 프리셋 파티 마스터가 있는가, `priority`가 겹치지 않는가, 프리셋 파티의 캐릭터 수가 `carry_limit`을 넘기지 않는가(넘기면 런타임에 침입 실패).
- [ ] 폐지 파일(`event_flow_condition_master`, `adv_flow_condition_master`, `communication_flow_master`)을 넣지 않았는가.

---

## 7. 코드 지도 (어디를 보면 되는가)

| 파일(`src/Wera.Core/`) | 책임 |
|---|---|
| `GamePack.cs` | `GamePack.Load`: 모든 로더 호출 순서, 필수 마스터 목록, 공용 도우미(`Body`, `TryReadEntry`, `ParseLong`, `OptionalLong`, `LoadCodeNames`, `LoadConditioned`) |
| `Tsv.cs` | TSV 파서와 `memo` 열 제거 |
| `GamePack.Layers.cs`, `PackMasterKeys.cs` | 모드(중첩 팩): `pack_master` 읽기, 레이어 합치기(`MergeTsv`), 마스터별 병합 키 등록(`PackMasterKeys.All`; 새 마스터는 여기에 키 등록) |
| `GamePack.<영역>.cs` | 마스터별 레코드, enum, 로더, 조회: `Skill`(EffectType, 스킬 실행), `SkillOrder`, `SkillAutoTarget`, `AfterHit/AfterAttack/AfterBattleWin/Lose/Run`, `AfterLoadCharacter/UniqueItem`, `CharacterAfterCreate`, `UniqueItemAfterCreate`, `XpType`, `SynthesisFlow`, `SmeltFlow`, `Event`, `AdvFlow`, `Communication`, `FacilityFlow`, `Defense`(방어력 흐름·시설 방어력), `DefenseEnemy`, `Facility`, `Hideout`, `Operator`, `DungeonMap`(타일/맵/인카운트/층), `Equipment`(태그·장비·예약 태그), `CharacterStatus`, `GameSetting`, `Preset`, `Turn`, `Mod`/`ModSkill`/`PartSlot`/`UniqueItemEnhanceStatus`/`OwnerStatus`, `Submodule`/`FlowSubmodule` |
| `Masters/MasterRows.cs`, `GamePack.Masters.cs` | MasterMemory 테이블 정의와 조회 |
| `UnitCircuitBase.cs` | 이펙트 서킷 공통(포커스, 캐시, 소수 버퍼, `Run`, 서브모듈, `ApplyOther`/`JumpOther` 훅) |
| `*Circuit.cs` | 각 서킷 실행기(스킬 3종, 피격/공격/승패도망, 로드 후, 생성 후, XP, 합성/제련, `EventFlowCircuit`, `AdvFlowCircuit`, `CommunicationFlowCircuit`, `FacilityFlowCircuit`, `DefenseFlowCircuit`) |
| `EffectArgs.cs`, `FlagJump.cs` | 이펙트 인자 검증(모든 이펙트 마스터 공유), 점프 비교/확률/태그 판정 도우미 |
| `GameSession.cs` | 세션 중심: `SaveData`, 저장/불러오기와 검증, 홈 메뉴, 화면 전이와 `EntryAction` 처리(`EndTurn` 포함), 파티, 디버그 |
| `GameSession.Instances.cs` | 인스턴스 ID 기반 캐릭터 보유, `carry_limit`, `AddCapturedEnemy`, 파티 배치 도우미 |
| `GameSession.Equipment.cs` | 유니크 아이템 인스턴스(ULID), 장비 착탈, 태그 |
| `GameSession.Battle.cs` / `BattleExecute.cs` / `BattleStatus.cs` | 전투 화면, 실행(`[107]`), 상태 확인/도망 |
| `GameSession.Defense.cs` | **방어전**: `RollDefenseEnemy` 이후 `ResolveDefense`/`RunDefense`/`BeginDefenseScreen`, 보스 자동 전투 |
| `GameSession.Hideout.cs` | 아지트 화면, 시설 짓기/철거, 오퍼레이터, **시설 서킷 `RunFacilityCircuits`**, `RollDefenseEnemy`, 위협도 |
| `GameSession.FacilityCheck.cs` | 시설 확인 화면 |
| `GameSession.Captive.cs` | 구금된 아군(`CaptureAlly`, `RescueCaptives`, `DungeonCaptives`) |
| `GameSession.Dungeon.cs` | 던전 이동, 타일 효과, 인카운트, 던전 이벤트 |
| `GameSession.EventScreen.cs` | 이벤트/ADV/교류 재생 화면(`AdvanceOnAnyKey` 포함) |
| `GameSession.Turn.cs` | 턴, `CanExplore`, `RecordExplore`/`RecordDefense` |
| `GameSession.Details.cs` | 상세 화면, 장비 강화(합성/제련/개조) 실행 |
| `GameSession.Skill.cs`, `Support.cs`, `Xp.cs`, `Paging.cs`, `AddInventory.cs`, `Debug.cs` | 스킬 설정, 파트너/어시스트, XP 표시, 목록 페이지, 인벤토리 추가 규칙, 디버그 |
| `Party.cs`, `PartyInstance.cs`, `Inventory.cs`, `CharacterCollection.cs`, `UnitStore.cs` | 캐릭터/유닛/파티 데이터 형태, 서킷의 저장소 |
| `DefenseDamage.cs` | 방어전 피해 `Roll`/`Distribute` |
| `src/Wera.Web/Pages/Home.razor` | 모든 화면의 HTML/이벤트 처리(960×900 고정) |
| `tests/Wera.Core.Tests/CoreTests.cs` | `PackZip`(최소 팩 보충), `Zip`, `PackZipWithout`; 나머지는 영역별 시험(방어전 `DefenseBattleTests`, 구금 `CapturedAllyTests`/`CapturedEnemyTests`, 서브모듈 `SubmoduleTests`/`FlowSubmoduleTests` …) |

---

## 부록 A. README와 코드가 어긋난 곳 (코드가 맞음)

README는 기능을 쌓아 오면서 일부 문단이 옛 상태로 남았습니다. **게임팩을 만들 때는 아래를 코드 기준으로 읽으세요.** (README 정리는 별도 작업으로 남아 있음)

1. **"아직 전투 실행에 연결되지 않았습니다 / 아직 전투에는 쓰이지 않음"**(스킬 마스터, 피격/공격/승리/패배/도망 후 이펙트, 스킬 순서 문단): 실제로는 **모두 전투에 연결**되어 있습니다(`GameSession.BattleExecute.cs`, `BattleStatus.cs`; 순서는 `SkillOrderCircuit`). 같은 README의 "전투 작전 화면" 문단이 정확합니다.
2. **`ActionOrder.Sort` / "전투 진행은 아직 없습니다"**: 전투 진행은 있고, `ActionOrder`는 쓰이지 않습니다(§4.3).
3. **태그 1/2/3은 "엔진이 예약한 코드"**(장비 문단): 현재는 **게임 설정이 코드를 정하고** 기본값이 1/2/3입니다(§2.1).
4. **합성/제련 고유 이펙트 번호**(합성·제련 흐름 문단의 32~35 표기): 서브모듈 도입 후 **33~36**입니다(`FocusSmeltTarget` 33 … `RandomToCache` 36, `FocusSynthesisTarget` 33 … `DeleteAllSacrifices` 36). README의 서브모듈 문단 끝의 번호 정리가 맞습니다.
5. **"합성을 실제로 실행하는 동작은 아직 없어"**: 합성·제련·개조 모두 장비 강화 화면에서 실행됩니다(`GameSession.Details.cs`).
6. **"구출은 아직 없습니다"**(구금된 아군 문단): 던전 이벤트의 `ReleaseCaptives`로 있습니다(§4.7).
7. **"로드 후 캐릭터 흐름 `LoadCharacterFlowType`은 지금 `None=0`뿐"**: 같은 문단 뒤쪽이 맞게 `EffectType` 0~32(공통 `JumpToSubmodule` 포함)와 같은 번호를 가집니다.
8. **"적을 포획하면 이 인스턴스를 보유 캐릭터로 옮길 예정"**(전투 문단): 일반 전투의 포획은 없고, 방어전에서 쓰러진 적의 구금만 구현되어 있습니다.
9. **README의 "이펙트 서킷 … 열한 개"**: 서브모듈이 붙는 종류는 15개입니다(§3.1 표).
10. 장비 강화 `Screen.EquipmentEnhance`의 enum 주석("내용은 아직 정의되지 않았다")은 옛 주석이고 화면은 구현되어 있습니다.
