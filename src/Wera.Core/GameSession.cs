using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wera.Core;

[JsonConverter(typeof(JsonStringEnumConverter<Screen>))]
public enum Screen
{
    Home,
    DungeonSelect,
    /// <summary>던전에 데려갈 파티 고르기. 돌아가기는 던전 선택.</summary>
    DungeonPartySelect,
    /// <summary>던전 이동 화면: 맵과 위·아래·왼쪽·오른쪽 한 칸씩 이동. 나가기는 홈.</summary>
    DungeonMove,
    /// <summary>게임 오버: 던전 전투에서 주인공이 있는 파티가 졌을 때. [999]로 로드 화면으로 간다.</summary>
    GameOver,
    ShopList,
    /// <summary>파티 설정(캐릭터 배치): 3×3 슬롯. 슬롯을 고르면 그 슬롯에 배치할 캐릭터 목록으로 간다.</summary>
    PartySetup,
    /// <summary>파티 이름 변경: 입력칸에 쓴 글이 새 파티 이름이 된다(Enter). 돌아가기는 들어온 화면(파티 설정 또는 장비 착용).</summary>
    PartyRename,
    /// <summary>파티 설정(장비 착용): 같은 3×3 슬롯. 슬롯을 고르면 그 캐릭터의 장비 목록으로 간다. 돌아가기는 홈.</summary>
    EquipmentSetup,
    /// <summary>파티 설정(리더 지정): 같은 3×3 슬롯. 리더 태그를 가진 캐릭터의 슬롯을 고르면 리더로 지정하거나 해제한다. 돌아가기는 홈.</summary>
    LeaderSetup,
    /// <summary>파티 설정(스킬 설정): 같은 3×3 슬롯. 캐릭터가 있는 슬롯을 고르면 그 캐릭터의 스킬 설정 화면으로 간다. 돌아가기는 홈.</summary>
    SkillSetup,
    /// <summary>선택한 슬롯 캐릭터의 스킬 슬롯(장착한 스킬)을 순서대로 보여 주고, 구분선 아래에 스킬 풀 버튼이 있다. 돌아가기는 스킬 설정.</summary>
    SkillCharacter,
    /// <summary>선택한 스킬 슬롯에 넣을 수 있는 스킬 풀 안의 스킬 목록. 고르면 장착한다. 돌아가기는 캐릭터의 스킬 설정.</summary>
    SkillSlotSelect,
    /// <summary>선택한 캐릭터가 지금까지 익힌 스킬 목록(스킬 풀). 돌아가기는 캐릭터의 스킬 설정.</summary>
    SkillPoolList,
    /// <summary>전투 화면: 이벤트의 Battle 칸이 연 전투의 작전 화면(슬롯 네 줄과 가운데 영역). 전투가 끝나면 돌아가기([999])로 전투를 시작한 화면에 돌아간다.</summary>
    Battle,
    /// <summary>전투 화면에서 슬롯 번호나 이미지를 눌러 간 캐릭터 상태 확인(상세) 페이지. 돌아가기는 전투.</summary>
    UnitStatus,
    /// <summary>선택한 슬롯 캐릭터의 장비 슬롯 목록. 돌아가기는 장비 착용 화면.</summary>
    EquipmentList,
    /// <summary>선택한 장비 슬롯에 낄 수 있는 장비 목록(슬롯 태그 마스터에 부합하는 장비). 돌아가기는 장비 슬롯 목록.</summary>
    EquipmentSelect,
    /// <summary>선택한 슬롯에 배치할 캐릭터 목록.</summary>
    CharacterSelect,
    /// <summary>아지트 방어전(턴 종료 때 적이 침입했을 때): 결과 로그를 Enter(코드 1)마다 한 줄씩 보여 준다. 다 보면 홈으로 돌아간다.</summary>
    Defense,
    /// <summary>아지트 화면(102): 층마다 계층/시설/보스 슬롯 3×3(한 번에 3개 층), 위쪽 줄은 "확장 가능"일 수 있다. 돌아가기는 홈.</summary>
    Hideout,
    /// <summary>시설 확인 화면: 아지트의 시설별 오퍼레이터, 방어력, 생산 내용을 읽기만 한다. 돌아가기는 아지트.</summary>
    FacilityCheck,
    /// <summary>아지트 확장 화면: 쓸 재료 그룹을 고른다(갖춘 그룹만 활성). 돌아가기는 아지트.</summary>
    HideoutExpand,
    /// <summary>아지트 층 상세(계층 슬롯): 기본 시설의 이름이 나온다. 돌아가기는 아지트.</summary>
    HideoutFloorDetail,
    /// <summary>아지트 시설 상세(가운데 슬롯, 지은 시설): 시설 이름과 철거. 돌아가기는 아지트.</summary>
    HideoutFacilityDetail,
    /// <summary>아지트 가운데 슬롯에 지을 시설 목록(설치 조건을 만족한 시설, 재료를 갖추지 못한 시설은 회색). 돌아가기는 아지트.</summary>
    HideoutFacilitySelect,
    /// <summary>고른 시설의 재료 그룹을 고르는 화면. 돌아가기는 시설 목록.</summary>
    HideoutBuild,
    /// <summary>시설의 오퍼레이터 슬롯에 세울 캐릭터 목록(슬롯의 태그 조건을 만족하는 캐릭터). 돌아가기는 그 시설 상세.</summary>
    HideoutOperatorSelect,
    /// <summary>장비 강화 화면(106): 내용은 아직 정의되지 않았다. 돌아가기는 홈.</summary>
    EquipmentEnhance,
    /// <summary>이벤트 화면(103): 조건을 만족하는 이벤트 목록. 하나를 고르면 그 이벤트를 시작한다.</summary>
    Event,
    /// <summary>진행 중인 이벤트의 현재 장면(ADV 재생 또는 전투). [1] 다음으로 이벤트를 이어 가며, 끝나면 이벤트 화면으로 돌아간다.</summary>
    EventPlay,
    /// <summary>교류 화면(104): 교류 그룹과 그 그룹의 교류 목록.</summary>
    Interaction,
    /// <summary>교류 진행 화면: 위쪽 슬롯은 그대로 두고 아래에서 고른 교류의 흐름을 재생한다.</summary>
    CommunicationPlay,
    /// <summary>디버그 화면(400): 캐릭터 추가, 장비 추가, 아이템 추가 버튼. 돌아가기는 홈.</summary>
    Debug,
    /// <summary>디버그: 마스터의 모든 캐릭터를 페이지당 20개씩 보여 주고 누르면 추가한다. 돌아가기는 디버그 화면.</summary>
    DebugAddCharacter,
    /// <summary>디버그: 마스터의 모든 유니크 아이템(장비)을 페이지당 20개씩 보여 주고 누르면 추가한다. 돌아가기는 디버그 화면.</summary>
    DebugAddEquipment,
    /// <summary>디버그: 마스터의 모든 커먼 아이템을 페이지당 20개씩 보여 주고 누르면 추가한다. 돌아가기는 디버그 화면.</summary>
    DebugAddItem,
    /// <summary>디버그 스킬 추가의 1단계: 가진 캐릭터 중 스킬을 받을 캐릭터를 고른다. 돌아가기는 디버그.</summary>
    DebugSkillCharacter,
    /// <summary>디버그 스킬 추가의 2단계: 마스터의 모든 스킬을 늘어놓고, 누르면 고른 캐릭터의 스킬 풀에 더한다. 돌아가기는 캐릭터 고르기.</summary>
    DebugAddSkill,
    /// <summary>스테이터스 확인 화면(던전 이동 화면의 [201]): 보는 파티의 슬롯을 파티 설정처럼 보여 주고, 캐릭터가 있는 슬롯을 고르면 캐릭터 상세 화면으로 간다.</summary>
    StatusCheck,
    /// <summary>캐릭터 상세 화면: 스테이터스 확인이나 동료 확인에서 고른 캐릭터. 내용은 아직 정의되지 않았다. 돌아가기는 연 화면.</summary>
    CharacterDetail,
    /// <summary>장비 상세 화면: 장비 확인에서 고른 장비. 내용은 아직 정의되지 않았다. 돌아가기는 장비 확인.</summary>
    EquipmentDetail,
    /// <summary>아이템 상세 화면: 아이템 확인에서 고른 커먼 아이템. 내용은 아직 정의되지 않았다. 돌아가기는 아이템 확인.</summary>
    ItemDetail,
    /// <summary>스킬 상세 화면: 캐릭터 상세의 스킬 슬롯 줄에서 고른 스킬. 내용은 아직 정의되지 않았다. 돌아가기는 연 화면.</summary>
    SkillDetail,
    /// <summary>동료 확인 화면(201): 가진 캐릭터 목록. 하나를 고르면 캐릭터 상세 화면으로 간다.</summary>
    CompanionCheck,
    /// <summary>장비 확인 화면(202): 가진 유니크 아이템 목록.</summary>
    EquipmentCheck,
    /// <summary>아이템 확인 화면(203): 가진 커먼 아이템과 수량 목록.</summary>
    ItemCheck,
    /// <summary>홈 화면의 파트너/어시스트 슬롯에 지정할 캐릭터 목록(파티 설정과 독립). 돌아가기는 홈.</summary>
    SupportSelect,
    /// <summary>선택한 상점의 교환 목록.</summary>
    ShopTrades,
    Load,
    /// <summary>개조를 고른 장비로 진행하는 화면: 장비 상세처럼 보이고 개조 슬롯 줄(1번부터)을 누르면 끼울 아이템을 고른다. 돌아가기는 장비 강화.</summary>
    EquipmentEnhanceTarget,
    /// <summary>개조 슬롯에 끼울 아이템을 고르는 화면: 그 슬롯에 끼울 수 있는 커먼 아이템 목록(보유한 것만 활성). 돌아가기는 개조 화면.</summary>
    EquipmentModSlot,
}

public enum EntryAction
{
    /// <summary>Target 화면으로 이동한다.</summary>
    Navigate,
    /// <summary>현재 상태를 세이브 파일로 내보낸다. 파일 저장은 UI가 맡고 화면은 바뀌지 않는다.</summary>
    Save,
    /// <summary>항목의 코드에 해당하는 상점 교환을 실행한다. 결과는 <see cref="GameSession.Message"/>.</summary>
    Trade,
    /// <summary>리더 지정 화면의 슬롯: Arg의 칸에 있는 유닛을 리더로 지정하거나(이미 리더면) 해제한다.</summary>
    ToggleLeader,
    /// <summary>전투를 마치고 전투를 시작한 화면으로 돌아간다. 이벤트의 Battle 칸이 연 전투였다면 그 칸 다음부터 이어 간다.</summary>
    EndBattle,
    /// <summary>이전(-1)/다음(1) 캐릭터(Arg)로 보는 캐릭터를 옮긴다.</summary>
    BattleSelectCharacter,
    /// <summary>행동 설정: 커맨드를 보는 캐릭터의 행동 후보로 바꾸고 화면을 적 3 / 아군 1로 바꾼다.</summary>
    BattleOpenActions,
    /// <summary>행동 후보에서 스킬(Arg)을 고른다.</summary>
    BattleChooseSkill,
    /// <summary>[105] 이동: 커맨드를 대상 후보처럼 바꿔 이동할 아군 슬롯(빈 칸 포함)을 슬롯에서 고르게 한다.</summary>
    BattleOpenMove,
    /// <summary>이동 후보에서 슬롯(Arg = 슬롯 번호)을 골라 보는 캐릭터의 이번 턴 행동을 그 슬롯으로의 이동으로 정한다.</summary>
    BattleMoveTo,
    /// <summary>[103] 아이템: 커맨드를 보는 캐릭터가 쓸 수 있는 아이템 후보로 바꾸고 화면을 적 3 / 아군 1로 바꾼다.</summary>
    BattleOpenItems,
    /// <summary>아이템 후보에서 아이템(Arg = "C코드"(커먼) 또는 "U인스턴스ID"(유니크))을 고른다.</summary>
    BattleChooseItem,
    /// <summary>행동 후보에서 나와 기본 커맨드로 돌아간다.</summary>
    BattleCancelActions,
    /// <summary>대상 후보에서 유닛을 고른다(Arg = "E슬롯"(적) 또는 "A슬롯"(아군)).</summary>
    BattleChooseTarget,
    /// <summary>[107] 실행: 이번 턴의 행동을 순서대로 실행한다.</summary>
    BattleExecute,
    /// <summary>전투 화면의 슬롯 번호: 그 캐릭터의 상태 확인 페이지로 간다(Arg = "E슬롯"(적) 또는 "A슬롯"(아군)).</summary>
    BattleViewUnit,
    /// <summary>[106] 도망: 전투를 끝낸다.</summary>
    BattleRun,
    /// <summary>작전 화면을 위(-1)나 아래(1) 구성으로 바로 넘긴다(Arg): 적 3 / 아군 1 ↔ 적 1 / 아군 3.</summary>
    BattleScroll,
    /// <summary>파티 이름 변경 화면으로 간다(들어온 화면을 기억해 두었다가 끝나면 돌아간다).</summary>
    RenameParty,
    /// <summary>파티 선택 버튼: Arg의 번호(0이 파티-A)의 파티를 보는 파티로 한다. 화면은 그대로다.</summary>
    SelectParty,
    /// <summary>던전 선택 화면에서 던전(항목의 코드)을 고르고 파티 선택 화면으로 간다.</summary>
    SelectDungeon,
    /// <summary>던전 파티 선택 화면에서 파티(Arg의 번호)를 골라 던전의 도입 지점으로 들어간다.</summary>
    EnterDungeon,
    /// <summary>항목의 코드에 해당하는 캐릭터를 선택한 슬롯에 배치한다(이미 배치된 캐릭터면 위치 교환).</summary>
    PlaceCharacter,
    /// <summary>선택한 슬롯의 캐릭터를 파티에서 뺀다(슬롯이 빈다).</summary>
    ClearSlot,
    /// <summary>아지트의 표시 층 범위를 Arg(-1 또는 1)층만큼 옮긴다.</summary>
    HideoutScroll,
    /// <summary>항목의 코드(그룹 코드)의 재료로 아지트를 확장한다.</summary>
    ExpandHideout,
    /// <summary>항목의 코드에 해당하는 캐릭터를 고른 오퍼레이터 슬롯에 세운다.</summary>
    SetHideoutOperator,
    /// <summary>고른 오퍼레이터 슬롯을 비운다.</summary>
    ClearHideoutOperator,
    /// <summary>항목의 코드에 해당하는 시설을 지으려고 고른다(재료 그룹이 없으면 바로 짓는다).</summary>
    SelectFacility,
    /// <summary>항목의 코드(그룹 코드)의 재료로 고른 시설을 짓는다.</summary>
    BuildFacility,
    /// <summary>고른 층의 가운데 슬롯의 시설을 철거한다.</summary>
    DemolishFacility,
    /// <summary>Arg의 층(0이 1층)의 방어 파티를 파티 설정 화면에서 편집한다(보스 슬롯).</summary>
    EditHideoutParty,
    /// <summary>항목의 코드에 해당하는 캐릭터를 홈의 파트너/어시스트 슬롯에 지정한다(다른 쪽에 있던 캐릭터면 서로 바뀐다).</summary>
    SetSupport,
    /// <summary>선택한 파트너/어시스트 슬롯을 비운다.</summary>
    ClearSupport,
    /// <summary>항목의 코드에 해당하는 보유 장비를 선택한 캐릭터의 선택한 장비 슬롯에 끼운다(이미 끼고 있던 장비는 빠진다).</summary>
    EquipItem,
    /// <summary>선택한 캐릭터의 선택한 장비 슬롯의 장비를 뺀다.</summary>
    Unequip,
    /// <summary>방어전 화면의 로그를 한 줄 더 보여 준다(더 없으면 방어전을 끝내고 홈으로 돌아가 시설 서킷을 돌린다).</summary>
    NextDefenseStep,
    /// <summary>항목의 코드에 해당하는 이벤트를 시작해 첫 장면으로 간다.</summary>
    StartEvent,
    /// <summary>진행 중인 이벤트를 다음 장면으로 이어 간다(더 없으면 이벤트 화면으로).</summary>
    NextEventStep,
    /// <summary>ADV의 선택지 장면에서 항목이 가리키는(Arg = 0부터 센 번호) 선택지를 고른다.</summary>
    ChooseAdv,
    /// <summary>교류 화면에서 항목의 코드(그룹 코드×100)에 해당하는 교류 그룹을 고른다. 화면은 그대로다.</summary>
    SelectCommunicationGroup,
    /// <summary>교류 화면에서 항목의 코드에 해당하는 교류를 시작해 흐름을 재생한다.</summary>
    StartCommunication,
    /// <summary>스킬 슬롯 선택 화면에서 스킬 풀의 스킬(Arg = 풀 번호)을 그 슬롯에 장착한다.</summary>
    EquipSkill,
    /// <summary>스킬 슬롯 선택 화면에서 그 슬롯의 스킬을 뺀다.</summary>
    ClearSkillSlot,
    /// <summary>디버그 추가 화면에서 항목(Arg = 마스터 코드)을 하나 추가한다. 화면은 그대로다.</summary>
    DebugAdd,
    /// <summary>디버그 스킬 추가에서 스킬을 받을 캐릭터(Arg = 캐릭터 코드)를 고른다.</summary>
    DebugSelectSkillCharacter,
    /// <summary>목록 화면의 페이지를 옮긴다(Arg = 페이지 번호, 1부터). 화면은 그대로다.</summary>
    ListPage,
    /// <summary>상세 화면을 연다(Arg = "C:캐릭터 코드", "S:파티 슬롯", "E:장비 인스턴스 ID", "I:아이템 코드", "K:스킬 코드"). 연 화면이 돌아갈 화면이 된다.</summary>
    OpenDetail,
    /// <summary>상세 화면의 돌아가기: 상세 화면을 열기 전의 화면과 그 화면이 보던 것으로 돌아간다.</summary>
    CloseDetail,
    /// <summary>장비 강화 화면에서 합성/제련/개조 중 하나를 골라 둔다(Arg = EnhanceMode 이름). 화면은 그대로다.</summary>
    SelectEnhanceMode,
    /// <summary>합성에서 제물로 쓸 장비를 하나 고른다(Arg = 장비 인스턴스 ID).</summary>
    SelectSacrifice,
    /// <summary>합성을 실행한다(제물을 다 골랐을 때만 활성).</summary>
    ExecuteSynthesis,
    /// <summary>합성/제련의 타겟과 제물을 비운다.</summary>
    CancelSynthesis,
    /// <summary>제련을 실행한다.</summary>
    ExecuteSmelt,
    /// <summary>골라 둔 합성/제련/개조로 진행할 장비를 고른다(Arg = 장비 인스턴스 ID).</summary>
    PickEnhanceEquipment,
    /// <summary>턴을 하나 넘긴다(<see cref="GameSession.AdvanceTurn"/>). 홈 화면에 남는다.</summary>
    EndTurn,
    /// <summary>개조 화면에서 개조 슬롯(Arg=슬롯 코드)을 골라 끼울 아이템 목록으로 간다.</summary>
    SelectModSlot,
    /// <summary>끼울 아이템(Arg=아이템 코드)을 골라 개조 슬롯에 끼운다: 인벤토리에서 1개를 쓰고 이미 낀 것은 인벤토리로 돌아온다.</summary>
    InstallMod,
    /// <summary>개조 슬롯에 낀 아이템을 빼서 인벤토리로 돌려준다.</summary>
    RemoveMod,
}

/// <summary>장비 강화 화면에서 먼저 골라 두는 작업.</summary>
public enum EnhanceMode { Synthesis, Smelt, Modify }

/// <summary>화면에 나오는 클릭 가능한 텍스트. 클릭하거나 Code를 입력하면 Action을 수행한다.</summary>
/// <param name="Arg">항목이 가리키는 추가 값(파티 슬롯 항목에서는 슬롯 번호 0~8).</param>
/// <param name="LabelHtml">꾸밈 문법(굵게, 색)이 적용된 문구의 HTML. 없으면 Label을 그대로 보여 준다(ADV 선택지가 쓴다).</param>
/// <param name="DataCode">목록 화면에서 Code가 페이지 안의 줄 번호일 때 그 항목 원래의 코드(이벤트 코드, 캐릭터 코드 등). 항목의 동작은 이 코드를 쓴다.</param>
/// <param name="Enabled">false면 회색으로 보이고 눌러도(코드를 입력해도) 아무 일도 없다(지금 할 수 없는 상점 교환).</param>
public sealed record ScreenEntry(
    string Code, string Label, Screen Target, EntryAction Action = EntryAction.Navigate, string? Arg = null, string? LabelHtml = null,
    bool Enabled = true, string? DataCode = null);

/// <param name="Version">릴리스 전이라 1로 유지한다. 지시가 있을 때까지 올리지 않는다.</param>
/// <param name="Characters">보유 캐릭터: 인스턴스 ID(ULID) → 인스턴스. 키가 ULID가 아닌 정수면 캐릭터 코드를 뜻하는 줄임 표기로 보고(그 코드의 인스턴스 하나) 새 ID를 채운다.</param>
/// <param name="StatusEffects">상태이상: 캐릭터 인스턴스 ID(또는 코드 줄임 표기) → 목록.</param>
/// <param name="CharacterTags">캐릭터 태그: 캐릭터 인스턴스 ID(또는 코드 줄임 표기) → 태그 목록.</param>
/// <param name="EquippedItems">예전 형식의 장비(캐릭터 인스턴스 ID 또는 코드 줄임 표기 → 슬롯별 장비). 새 세이브는 파티의 유닛이 장비를 가진다.</param>
/// <param name="PartnerId">파트너 캐릭터 인스턴스 ID. 예전 형식의 <paramref name="Partner"/>(캐릭터 코드)는 그 코드의 첫 인스턴스로 읽는다.</param>
public sealed record SaveData(
    int Version,
    string PackId,
    Screen Screen = Screen.Home,
    Dictionary<long, long>? PlayerFlags = null,
    Dictionary<long, InventoryItem>? InventoryItems = null,
    Dictionary<string, CharacterInstance>? Characters = null,
    long?[]? Party = null,
    Dictionary<string, List<ActiveStatusEffect>>? StatusEffects = null,
    Dictionary<string, UniqueItemInstance>? UniqueItems = null,
    Dictionary<string, List<long>>? CharacterTags = null,
    Dictionary<string, Dictionary<long, EquipRef>>? EquippedItems = null,
    long? Partner = null,
    long? Assist = null,
    long? Turn = null,
    PartyInstance? PartyInstance = null,
    PartyInstance?[]? Parties = null,
    int? CurrentParty = null,
    PartyInstance?[]? HideoutParties = null,
    long?[]? HideoutFacilities = null,
    HideoutOperatorSave[]? HideoutOperators = null,
    string? PartnerId = null,
    string? AssistId = null,
    Dictionary<long, PartyUnit[]>? DungeonCaptives = null,
    long? CurrentHideout = null,
    Dictionary<long, HideoutSave>? OtherHideouts = null);

/// <summary>
/// 세이브의 아지트 하나의 상태(<see cref="SaveData.OtherHideouts"/>의 값): <see cref="SaveData.HideoutParties"/>, <see cref="SaveData.HideoutFacilities"/>,
/// <see cref="SaveData.HideoutOperators"/>와 같은 모양이다. 현재 아지트의 상태는 그 세 필드에 있고, 나머지 아지트의 상태가 여기 있다(아지트 마스터가 없으면 쓰지 않는다).
/// </summary>
public sealed record HideoutSave(PartyInstance?[]? Parties = null, long?[]? Facilities = null, HideoutOperatorSave[]? Operators = null);

/// <summary>세이브의 오퍼레이터 한 명: 층이 생긴 순서(1은 처음부터 있는 층), 지은 시설인가(아니면 기본 시설), 오퍼레이터 슬롯 코드, 캐릭터 코드, 캐릭터 인스턴스 ID(비어 있으면 코드의 첫 인스턴스).</summary>
public sealed record HideoutOperatorSave(long FloorOrder, bool Built, long SlotCode, long CharacterCode, string InstanceId = "");

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

public sealed class SaveFormatException(string message) : Exception(message);

/// <summary>메뉴의 한 분류: 이름(화면에는 [이름]으로 나온다)과 그 아래 항목들.</summary>
public sealed record MenuGroup(string Name, IReadOnlyList<ScreenEntry> Entries);

public sealed partial class GameSession
{
    public const int SaveVersion = 1;

    public const string DungeonEntryCode = "101";
    public const string HideoutEntryCode = "102";
    public const string EventEntryCode = "103";
    public const string ShopEntryCode = "104";
    public const string PartyEntryCode = "105";
    public const string EquipmentEnhanceEntryCode = "106";
    public const string InteractionEntryCode = "107";
    public const string EndTurnEntryCode = "108";
    /// <summary>장비 강화 화면의 합성/제련/개조 항목(장비 목록 아래, 페이지와 상관없이 늘 나온다).</summary>
    public const string SynthesisEntryCode = "901";
    public const string SmeltEntryCode = "902";
    public const string ModifyEntryCode = "903";
    /// <summary>합성에서 타겟을 고른 뒤 합성/제련/개조 자리에 나오는 실행과 취소(같은 자리, 같은 코드).</summary>
    public const string ExecuteEntryCode = "901";
    public const string CancelEntryCode = "902";
    public const string SaveEntryCode = "200";
    public const string CompanionCheckEntryCode = "201";
    public const string EquipmentCheckEntryCode = "202";
    public const string ItemCheckEntryCode = "203";
    public const string LoadEntryCode = "300";
    public const string DebugEntryCode = "400";
    public const string BackEntryCode = "999";
    /// <summary>개조 슬롯의 아이템 목록에서 낀 아이템을 빼는 항목의 코드.</summary>
    public const string RemoveModEntryCode = "998";

    /// <summary>캐릭터 선택 화면의 "[0] 비우기": 선택한 슬롯에 캐릭터가 있을 때만 나온다.</summary>
    public const string ClearEntryCode = "0";

    /// <summary>파티 설정 화면 아래의 텍스트 버튼: 캐릭터 배치 화면 / 장비 착용 화면으로 바꾼다.</summary>
    public const string PlacementModeCode = "101";
    public const string EquipmentModeCode = "102";
    public const string SkillModeCode = "103";
    public const string LeaderModeCode = "104";

    /// <summary>파티 선택 버튼의 코드는 파티 A가 11이고 파티 P가 26이다(<see cref="PartySelectCodeBase"/> + 파티 번호).</summary>
    public const int PartySelectCodeBase = 11;

    /// <summary>파티 설정 화면 제목의 "[0] 파티명": 누르면 파티 이름을 바꾼다.</summary>
    public const string RenamePartyEntryCode = "0";

    /// <summary>파티 이름의 최대 글자 수.</summary>
    public const int MaxPartyNameLength = 20;

    /// <summary>파티 이름을 바꾸고 돌아갈 화면.</summary>
    Screen partyRenameReturn = Screen.PartySetup;

    /// <summary>플레이어가 가진 파티 수: 파티-A ~ 파티-P. 파티끼리는 캐릭터도 장비도 공유할 수 없다(다른 전투에 파견하기 위함).</summary>
    public const int PartyCount = 16;

    /// <summary>파티 슬롯 수: 전위/중위/후위 3줄 × 3칸.</summary>
    public const int PartySlotCount = 9;

    public GamePack Pack { get; }
    public Screen Screen { get; private set; }
    /// <summary>플레이어_플래그: 플래그 코드 → long 값.</summary>
    public Dictionary<long, long> PlayerFlags { get; }

    /// <summary>인벤토리_아이템: 아이템 코드 → 구조체.</summary>
    public Dictionary<long, InventoryItem> InventoryItems { get; }

    /// <summary>
    /// 보유 캐릭터 인스턴스: 인스턴스 ID(ULID 문자열) → 현재 상태. 인스턴스의 캐릭터 코드는 캐릭터 인벤토리 마스터에 있어야 하고,
    /// 같은 코드는 마스터의 carry_limit개까지 가질 수 있다. 코드(long)로도 읽고 쓸 수 있지만 그것은 "그 코드의 첫 인스턴스"를 뜻한다.
    /// </summary>
    public CharacterCollection Characters { get; }

    /// <summary>
    /// 파티: 이름과 유닛 아홉 칸(유닛 = 캐릭터 + 장비). 칸 번호 0~2 전위, 3~5 중위, 6~8 후위, 빈 칸은 null.
    /// 장비는 유닛에 속하므로 유닛이 파티에서 빠지면 장비는 보유로 돌아간다.
    /// </summary>
    public PartyInstance PartyInstance => hideoutEditFloor is { } floor ? hideoutParties[floor] : Parties[CurrentPartyIndex];

    /// <summary>
    /// 파티 열여섯 개(파티-A ~ 파티-P). 파티끼리는 캐릭터와 장비를 공유할 수 없다. 파티 설정과 캐릭터 배치, 장비 착용은
    /// 지금 보는 파티(<see cref="CurrentPartyIndex"/>)를 따른다.
    /// </summary>
    public PartyInstance[] Parties { get; }

    /// <summary>지금 보는 파티의 번호(0이 파티-A). 새 게임은 파티-A다.</summary>
    public int CurrentPartyIndex { get; private set; }

    /// <summary>지금 보는 파티의 이름.</summary>
    public string PartyName
    {
        get => hideoutEditFloor is { } floor ? $"{floor + 1}층" : PartyInstance.Name;
        set { if (hideoutEditFloor is null) Parties[CurrentPartyIndex] = PartyInstance with { Name = value }; }
    }

    /// <summary>파티의 처음 이름: "파티-A" ~ "파티-P".</summary>
    public static string DefaultPartyNameOf(int index) => "파티-" + (char)('A' + index);

    /// <summary>이 캐릭터 코드의 유닛이 있는 파티의 번호(어느 파티에도 없으면 -1).</summary>
    public int PartyIndexOf(long characterCode)
    {
        for (var i = 0; i < Parties.Length; i++)
            if (Parties[i].IndexOf(characterCode) >= 0) return i;
        return -1;
    }

    /// <summary>이 인스턴스의 유닛이 있는 파티의 번호(어느 파티에도 없으면 -1).</summary>
    public int PartyIndexOf(string instanceId)
    {
        for (var i = 0; i < Parties.Length; i++)
            if (IndexOfId(Parties[i], instanceId) >= 0) return i;
        return -1;
    }

    /// <summary>이 캐릭터 코드의 유닛(어느 파티든, 같은 코드가 여럿이면 앞의 것). 파티에 없으면 null.</summary>
    public PartyUnit? UnitOf(long characterCode) =>
        AllParties.Select(p => p.UnitOf(characterCode)).FirstOrDefault(u => u is not null);

    /// <summary>이 인스턴스의 유닛(어느 파티든). 파티에 없으면 null.</summary>
    public PartyUnit? UnitOf(string instanceId) =>
        AllParties.Select(p => IndexOfId(p, instanceId) is var at and >= 0 ? p.Units[at] : null).FirstOrDefault(u => u is not null);

    /// <summary>칸 번호로 캐릭터 코드를 읽고 쓰는 창(<see cref="PartySlots"/>): 길이 9, 빈 칸은 null. 코드를 쓰면 아직 배치하지 않은 그 코드의 첫 인스턴스가 들어간다.</summary>
    public PartySlots Party => new(PartyInstance, code => PlaceableInstanceOf(code));

    /// <summary>
    /// 캐릭터에 걸려 있는 상태이상: 캐릭터 인스턴스 ID → 상태이상 목록. 각 이펙트의 시점, 우선도, 이펙트 코드, 밸류 1~3, 카운터, 유효 플래그를
    /// 그대로 들고 있어 세이브에서 복구할 수 있다. 비어 있는 캐릭터는 키가 없다.
    /// </summary>
    public Dictionary<string, List<ActiveStatusEffect>> CharacterStatusEffects { get; }

    /// <summary>교환 목록 화면에서 보고 있는 상점 코드. 그 화면이 아니면 null.</summary>
    public long? CurrentShopCode { get; private set; }

    /// <summary>교류 화면의 항목 코드는 그룹 코드에 이 값을 곱한 것이다("[{code * 100}] {name}").</summary>
    public const long CommunicationGroupEntryFactor = 100;

    /// <summary>교류 그룹들(코드 순서: 가장 작은 코드가 위).</summary>
    public IReadOnlyList<CommunicationGroupEntry> CommunicationGroups =>
        (Pack.CommunicationGroups ?? []).OrderBy(g => g.Code).ToList();

    /// <summary>교류 화면에서 고른 그룹의 코드. 화면에 들어가면 가장 작은 그룹이고 그룹이 없으면 null. 저장하지 않는다.</summary>
    public long? CurrentCommunicationGroupCode { get; private set; }

    /// <summary>캐릭터 선택 화면에서 배치 중인 슬롯(0~8). 그 화면이 아니면 null.</summary>
    public int? CurrentSlot { get; private set; }

    /// <summary>선택한 슬롯(<see cref="CurrentSlot"/>)에 있는 캐릭터의 이름. 슬롯이 없으면 null, 빈 슬롯이면 "빈 슬롯".</summary>
    public string? CurrentSlotCharacterName => CurrentSlot is { } slot ? PartySlotViews[slot].Name : null;

    /// <summary>선택한 슬롯(<see cref="CurrentSlot"/>)에 있는 캐릭터의 코드. 슬롯이 없거나 비어 있으면 null.</summary>
    public long? CurrentCharacterCode => CurrentSlot is { } slot ? PartyInstance.Units[slot]?.CharacterCode : null;

    /// <summary>선택한 슬롯(<see cref="CurrentSlot"/>)에 있는 캐릭터의 인스턴스 ID. 슬롯이 없거나 비어 있으면 null.</summary>
    public string? CurrentCharacterId => CurrentSlot is { } slot && PartyInstance.Units[slot] is { } unit ? IdOf(unit) : null;

    /// <summary>장비 가능 목록 화면에서 보고 있는 장비 슬롯 코드. 그 화면이 아니면 null.</summary>
    public long? CurrentEquipmentSlotCode { get; private set; }

    public EquipmentSlotEntry? CurrentEquipmentSlot =>
        CurrentEquipmentSlotCode is { } slotCode ? Pack.FindEquipmentSlot(slotCode) : null;

    /// <summary>선택한 캐릭터가 가진 장비 슬롯들.</summary>
    public IReadOnlyList<EquipmentSlotEntry> CurrentEquipmentSlots =>
        CurrentCharacterCode is { } character ? Pack.EquipmentSlotsOf(character) : [];

    GameSession(
        GamePack pack, Screen screen, Dictionary<long, long> playerFlags,
        Dictionary<long, InventoryItem> inventoryItems, CharacterCollection characters, PartyInstance[] parties,
        Dictionary<string, List<ActiveStatusEffect>> statusEffects,
        Dictionary<string, UniqueItemInstance> uniqueItems, Dictionary<string, List<long>> characterTags)
    {
        Pack = pack;
        Screen = screen;
        PlayerFlags = playerFlags;
        InventoryItems = inventoryItems;
        Characters = characters;
        Parties = parties;
        CharacterStatusEffects = statusEffects;
        UniqueItemInstances = uniqueItems;
        CharacterTagInstances = characterTags;
    }

    /// <summary>다음에 얻는 캐릭터의 OrderNumber: 소지 캐릭터 수(0부터 시작). 순서를 바꿀 때는 OrderNumber를 직접 바꾼다.</summary>
    static long NextOrderNumber(IEnumerable<CharacterInstance> owned) => owned.Count();

    /// <summary>
    /// 지금 가진 캐릭터 인스턴스 전부(플레이어 포함), OrderNumber순이고 같으면 InstanceId순(동료 확인 화면의 목록).
    /// 같은 캐릭터를 여럿 가졌으면 인스턴스마다 한 줄이다. 던전에 갇힌 구금된 아군은 나오지 않는다.
    /// </summary>
    public IReadOnlyList<CharacterInstance> OwnedCharacters =>
        Characters.Values.Where(c => !IsCapturedAlly(c.InstanceId)).OrderBy(c => c.OrderNumber).ThenBy(c => c.InstanceId, StringComparer.Ordinal).ToList();

    /// <summary>동료 확인 코드 칸("[OrderNumber]")의 너비(반각 칸 수). 코드 뒤는 이름 앞까지 공백으로 채운다.</summary>
    public const int CompanionCodeColumns = 8;

    /// <summary>동료 확인 이름 칸의 너비(반각 칸 수; 한글 등 전각 문자는 2칸).</summary>
    public const int CompanionNameColumns = 32;

    /// <summary>동료 확인 Lv, Hp, Xpp 칸 하나의 너비(반각 칸 수). 칸 안에는 값만 왼쪽 맞춤으로 들어간다(이름표는 첫 줄의 열 이름).</summary>
    public const int CompanionStatColumns = 8;

    /// <summary>동료 확인 화면의 한 줄: 코드 칸("[순서 번호]"), 이름, 그리고 레벨, HP, XPP 값 칸.</summary>
    public sealed record CompanionRow(string Order, string Name, string Level, string Hp, string Xpp);

    /// <summary>
    /// 동료 확인 화면의 줄들: <see cref="OwnedCharacters"/> 순서대로 "[OrderNumber] 이름 Lv 레벨 Hp HP Xpp XPP".
    /// 코드 칸은 반각 <see cref="CompanionCodeColumns"/>칸, 이름 칸은 반각 <see cref="CompanionNameColumns"/>칸, Lv, Hp, Xpp 칸은 반각 <see cref="CompanionStatColumns"/>칸씩이고 모두 왼쪽 맞춤이다
    /// (코드와 이름 사이는 공백으로 채운다). 화면은 이 칸 수(ch) 고정 너비로 그려서 글꼴과 상관없이 칸이 맞는다.
    /// </summary>
    public IReadOnlyList<CompanionRow> Companions
    {
        get
        {
            return OwnedCharacters.Select(c => new CompanionRow(
                $"[{c.OrderNumber.ToString(CultureInfo.InvariantCulture)}]",
                Pack.FindCharacter(c.CharacterCode)?.Name ?? Text(c.CharacterCode),
                c.Level.ToString(CultureInfo.InvariantCulture),
                c.Hp.ToString(CultureInfo.InvariantCulture),
                c.Xpp.ToString(CultureInfo.InvariantCulture))).ToList();
        }
    }

    /// <summary>확인 화면(동료/장비/아이템)의 첫 줄에 나오는 열 이름들: 줄과 같은 칸 너비로 맨 위에 한 번 나오고 눌러도 아무 일이 없다.</summary>
    public static readonly IReadOnlyList<string> CompanionColumnNames = ["#", "이름", "레벨", "체력", "XP"];
    public static readonly IReadOnlyList<string> EquipmentColumnNames = ["#", "이름"];
    public static readonly IReadOnlyList<string> ItemColumnNames = ["#", "이름", "수량"];

    /// <summary>동료 확인 화면의 줄들을 글로 쓴 것(각 칸을 정해진 칸 수까지 공백으로 채움). 화면은 <see cref="Companions"/>로 같은 줄을 그린다.</summary>
    public IReadOnlyList<string> CompanionLines =>
        Companions.Select(r => $"{PadColumns(r.Order, CompanionCodeColumns)} {PadColumns(r.Name, CompanionNameColumns)} {PadColumns(r.Level, CompanionStatColumns)} " +
                               $"{PadColumns(r.Hp, CompanionStatColumns)} {PadColumns(r.Xpp, CompanionStatColumns)}").ToList();

    /// <summary>아이템 확인 수량 칸의 너비(반각 칸 수). 칸 안에는 값만 왼쪽 맞춤으로 들어간다(이름표는 첫 줄의 열 이름).</summary>
    public const int ItemAmountColumns = 16;

    /// <summary>아이템 확인 화면의 한 줄: 코드 칸("[아이템 코드]"), 이름, 수량 값 칸.</summary>
    public sealed record ItemRow(string Code, string Name, string Amount);

    /// <summary>
    /// 아이템 확인 화면의 줄들: 지금 가진(수량이 1 이상인) 커먼 아이템을 커먼 아이템 인벤토리 마스터 순서대로 "[아이템 코드] 이름 수량 N".
    /// 칸 너비는 동료 확인과 같고(코드 <see cref="CompanionCodeColumns"/>, 이름 <see cref="CompanionNameColumns"/>) 수량 칸은 <see cref="ItemAmountColumns"/>칸이다.
    /// 장비에 끼워 둔 것도 가진 수량에 포함된다.
    /// </summary>
    public IReadOnlyList<ItemRow> Items =>
        (Pack.ItemInventory ?? [])
            .Where(i => InventoryItems.GetValueOrDefault(i.Code).Amount > 0)
            .Select(i => new ItemRow(
                $"[{i.Code.ToString(CultureInfo.InvariantCulture)}]",
                i.Name,
                InventoryItems[i.Code].Amount.ToString(CultureInfo.InvariantCulture))).ToList();

    static string PadColumns(string text, int columns) => text + new string(' ', Math.Max(0, columns - DisplayColumns(text)));

    /// <summary>글이 차지하는 칸 수(한글 등 전각 문자는 2칸).</summary>
    static int DisplayColumns(string text) => text.EnumerateRunes().Sum(RichText.ColumnsOf);

    /// <summary>
    /// 캐릭터를 새로 얻는다: 캐릭터 인벤토리 마스터에 있고 그 코드의 보유 수가 마스터의 carry_limit보다 적을 때, 새 인스턴스를 만들어
    /// 캐릭터 스테이터스 마스터의 레벨 0 값으로 상태를 채워 보유 캐릭터에 넣는다. 태그는 캐릭터 태그 마스터에서 복사한다.
    /// 마스터에 없거나 소지 한도(플레이어는 늘 1)에 닿았으면 false.
    /// </summary>
    public bool AddCharacter(long characterCode) => AddCharacterInstance(characterCode) is not null;

    /// <summary><see cref="AddCharacter"/>와 같되 새 인스턴스의 ID를 돌려준다(얻지 못하면 null).</summary>
    public string? AddCharacterInstance(long characterCode)
    {
        if (Pack.FindCharacter(characterCode) is null || !CanAddCharacter(characterCode)) return null;
        var id = NewInstanceId();
        Characters[id] = new CharacterAfterCreateCircuit(Pack,
            Pack.NewCharacterInstance(characterCode, id, NextOrderNumber(Characters.Values))).Execute() with { InstanceId = id };
        CharacterTagInstances[id] = Pack.MasterTagsOfCharacter(characterCode).ToList();
        return id;
    }

    static PartyInstance[] NewParties() =>
        Enumerable.Range(0, PartyCount).Select(i => PartyInstance.Empty(DefaultPartyNameOf(i))).ToArray();

    /// <summary>튜토리얼이 구현될 때까지 새 게임은 홈 화면으로 직행한다.</summary>
    public static GameSession NewGame(GamePack pack)
    {
        var session = new GameSession(pack, Screen.Home, [], [], [], NewParties(), [], [], []);
        session.AddCharacter(GamePack.PlayerCharacterCode);      // 플레이어(캐릭터 1)는 처음부터 보유한다
        foreach (var itemCode in pack.DebugStartUniqueItems) session.AddUniqueItem(itemCode);   // 디버깅용 초기 부여
        session.TurnCount = pack.FirstTurn.TurnCount;            // 가장 작은 턴에서 시작한다
        session.BeginOpeningEvent();                             // 오프닝 이벤트(게임 설정)가 있으면 먼저 재생한다
        return session;
    }

    /// <summary>입장 조건을 하나라도 만족하는 던전.</summary>
    public IReadOnlyList<Dungeon> AvailableDungeons =>
        Pack.Dungeons?.Where(d => d.IsAvailable(PlayerFlags)).ToList() ?? [];

    /// <summary>입장 조건을 하나라도 만족하는 상점.</summary>
    public IReadOnlyList<Shop> AvailableShops =>
        Pack.Shops?.Where(s => s.IsAvailable(PlayerFlags)).ToList() ?? [];

    public Shop? CurrentShop => Pack.Shops?.FirstOrDefault(s => s.Code == CurrentShopCode);

    /// <summary>현재 상점에서 조건을 만족하는 교환. 아이템 코드는 이름으로 풀어 둔다.</summary>
    public IReadOnlyList<TradeLine> CurrentShopTrades =>
        CurrentShopCode is null
            ? []
            : Pack.Trades?
                .Where(t => t.ShopCode == CurrentShopCode && t.IsAvailable(PlayerFlags))
                .Select(t => new TradeLine(
                    t.Code,
                    Pack.TradeTargetName(t.CostKind, t.CostItemCode), t.CostAmount,
                    Pack.TradeTargetName(t.ReceiveKind, t.ReceiveItemCode), t.ReceiveAmount))
                .ToList() ?? [];

    string CharacterName(long code) =>
        Pack.FindCharacter(code)?.Name ?? code.ToString(CultureInfo.InvariantCulture);

    /// <summary>파티 설정 화면의 9개 슬롯. 비었거나 상태를 알 수 없으면 "빈 슬롯"(회색 0/0).</summary>
    public IReadOnlyList<PartySlotView> PartySlotViews =>
        Enumerable.Range(0, PartySlotCount).Select(SlotView).ToList();

    PartySlotView SlotView(int slot)
    {
        var unit = PartyInstance.Units[slot];
        var id = unit is null ? null : IdOf(unit);
        return CharacterView(slot, id) with
        {
            IsLeader = unit?.IsLeader == true,
            IsUnavailable = id is not null && Characters.ContainsKey(id) && !CanExplore(id),
        };
    }

    /// <summary>캐릭터 한 명(인스턴스)의 슬롯 표시. ID가 없거나 보유하지 않으면 빈 슬롯. 슬롯 번호가 없는 슬롯은 -1.</summary>
    PartySlotView CharacterView(int slot, string? instanceId)
    {
        if (instanceId is null || !Characters.TryGetValue(instanceId, out var state))
        {
            return PartySlotView.Empty(slot);
        }
        var code = state.CharacterCode;
        var xp = XpLine(state);
        return new PartySlotView(
            slot, code, $"Lv.{state.Level}", CharacterName(code),
            Gauge.Render(state.Hp, state.MaxHp, gray: state.IsDead),
            xp.Gauge, xp.Tag,
            AttributeBadges(code),
            StatusBadges(instanceId), InstanceId: instanceId);
    }

    /// <summary>캐릭터에 걸려 있는 상태이상의 배지(걸린 순서). 이름은 상태이상 마스터에서 찾는다.</summary>
    IReadOnlyList<StatusBadge> StatusBadges(string instanceId) =>
        StatusEffectsOf(instanceId)
            .Select(s => new StatusBadge(
                s.StatusEffectCode,
                Pack.FindStatusEffect(s.StatusEffectCode)?.Name
                    ?? s.StatusEffectCode.ToString(CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>캐릭터의 유리속성(⭕), 불리속성(❌) 순서의 배지.</summary>
    IReadOnlyList<AttributeBadge> AttributeBadges(long characterCode)
    {
        var character = Pack.FindCharacter(characterCode);
        if (character is null) return [];
        string NameOf(long attribute) =>
            Pack.FindAttribute(attribute)?.Name ?? attribute.ToString(CultureInfo.InvariantCulture);
        return
        [
            new AttributeBadge(character.AdvantageAttributeCode, NameOf(character.AdvantageAttributeCode), Advantage: true),
            new AttributeBadge(character.DisadvantageAttributeCode, NameOf(character.DisadvantageAttributeCode), Advantage: false),
        ];
    }

    /// <summary>
    /// 캐릭터 선택 화면의 목록(캐릭터 인벤토리 마스터 순서, 같은 캐릭터를 여럿 가졌으면 인스턴스마다 한 줄). 보유하지 않은 캐릭터는 목록에 나오지 않는다.
    /// 이 슬롯에 이미 있는 인스턴스는 목록에 나오지 않는다. 다른 슬롯에 있는 파티원은 파란색, 나머지는 흰색.
    /// </summary>
    public IReadOnlyList<CharacterListItem> CharacterList
    {
        get
        {
            if (Screen is Screen.SupportSelect or Screen.HideoutOperatorSelect) return SupportList;
            if (Screen != Screen.CharacterSelect || CurrentSlot is not { } slot) return [];
            // 파티에 아무도 없을 때와 리더의 슬롯을 바꿀 때는 리더 태그를 가진 캐릭터만 나온다(파티에는 리더가 한 명 이상 있어야 하므로).
            var leaderOnly = IsLeaderOnlySlot(slot);
            var here = PartyInstance.Units[slot] is { } current ? IdOf(current) : null;
            return OwnedByMasterOrder().Where(x => x.Instance.InstanceId != here && !IsCapturedEnemy(x.Instance.InstanceId) && (!leaderOnly || HasLeaderTag(x.Instance.InstanceId))).Select(x =>
            {
                var id = x.Instance.InstanceId;
                var color = IndexOfId(PartyInstance, id) >= 0 ? CharacterListColor.Green
                    : InOtherParty(id) ? CharacterListColor.Gray          // 다른 파티(방어 파티 포함)의 유닛: 파티끼리는 캐릭터를 공유할 수 없다
                    : CharacterListColor.White;
                return new CharacterListItem(x.Entry.Code, x.Entry.Name, color, id);
            }).ToList();
        }
    }

    /// <summary>보유 캐릭터 인스턴스를 캐릭터 인벤토리 마스터 순서로(같은 코드는 OrderNumber순, 같으면 InstanceId순) 늘어놓은 것.</summary>
    IEnumerable<(CharacterEntry Entry, CharacterInstance Instance)> OwnedByMasterOrder() =>
        (Pack.CharacterInventory ?? []).SelectMany(c => Characters.InstancesOf(c.Code).Where(i => !IsCapturedAlly(i.InstanceId)).Select(i => (c, i)));

    /// <summary>
    /// 홈 화면의 메뉴: 구분선으로 나뉜 세 분류. [RPG] 던전(101, 입장 가능한 것이 없으면 회색), 아지트(102), 이벤트(103),
    /// 상점(104, 열린 상점이 없으면 회색), 파티 설정(105), 장비 강화(106), 교류(107) / [정보] 동료 확인(201), 장비 확인(202), 아이템 확인(203) / [시스템] 세이브(200), 로드(300), 디버그(400).
    /// 홈의 <see cref="Entries"/>는 이 분류들을 순서대로 이어 붙인 것이다.
    /// </summary>
    public IReadOnlyList<MenuGroup> HomeMenu
    {
        get
        {
            var rpg = new List<ScreenEntry>();
            // 입장할 수 있는 던전/상점이 없으면 메뉴에서 빼지 않고 회색(눌러도 반응 없음)으로 둔다.
            rpg.Add(new ScreenEntry(DungeonEntryCode, "던전", Screen.DungeonSelect, Enabled: AvailableDungeons.Count > 0));
            rpg.Add(new ScreenEntry(HideoutEntryCode, "아지트", Screen.Hideout));
            rpg.Add(new ScreenEntry(EventEntryCode, "이벤트", Screen.Event));
            rpg.Add(new ScreenEntry(ShopEntryCode, "상점", Screen.ShopList, Enabled: AvailableShops.Count > 0));
            rpg.Add(new ScreenEntry(PartyEntryCode, "파티 설정", Screen.PartySetup));
            rpg.Add(new ScreenEntry(EquipmentEnhanceEntryCode, "장비 강화", Screen.EquipmentEnhance));
            rpg.Add(new ScreenEntry(InteractionEntryCode, "교류", Screen.Interaction, Enabled: CanStartCommunication));
            // 마지막 턴이라 넘길 다음 턴이 없으면 회색이다.
            rpg.Add(new ScreenEntry(EndTurnEntryCode, "턴 종료", Screen.Home, EntryAction.EndTurn, Enabled: Pack.NextTurnAfter(TurnCount) is not null));

            return
            [
                new MenuGroup("RPG", rpg),
                new MenuGroup("정보",
                [
                    new ScreenEntry(CompanionCheckEntryCode, "동료 확인", Screen.CompanionCheck),
                    new ScreenEntry(EquipmentCheckEntryCode, "장비 확인", Screen.EquipmentCheck),
                    new ScreenEntry(ItemCheckEntryCode, "아이템 확인", Screen.ItemCheck),
                ]),
                new MenuGroup("시스템",
                [
                    new ScreenEntry(SaveEntryCode, "세이브", Screen.Home, EntryAction.Save),
                    new ScreenEntry(LoadEntryCode, "로드", Screen.Load),
                    new ScreenEntry(DebugEntryCode, "디버그", Screen.Debug),
                ]),
            ];
        }
    }

    /// <summary>현재 화면에서 클릭하거나 코드를 입력할 수 있는 텍스트들. 목록 화면은 지금 페이지의 항목과 페이지 버튼만 담는다(<see cref="Paginate"/>).</summary>
    public IReadOnlyList<ScreenEntry> Entries => Paginate(RawEntries);

    /// <summary>페이지로 자르기 전의 화면 항목들: 목록 화면의 모든 항목이 들어 있다.</summary>
    IReadOnlyList<ScreenEntry> RawEntries
    {
        get
        {
            switch (Screen)
            {
                case Screen.Home:
                    return HomeMenu.SelectMany(group => group.Entries).ToList();
                case Screen.ShopList:
                    // 상점 코드를 입력하거나 클릭하면 그 상점의 교환 목록으로 간다.
                    return AvailableShops
                        .Select(s => new ScreenEntry(Text(s.Code), s.Name, Screen.ShopTrades))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.ShopTrades:
                    // 교환 코드를 입력하거나 클릭하면 그 교환을 실행한다.
                    return CurrentShopTrades
                        .Select(t => new ScreenEntry(Text(t.Code), t.Text, Screen.ShopTrades, EntryAction.Trade, Enabled: CanTrade(t.Code)))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.ShopList))
                        .ToList();
                case Screen.PartySetup:
                    // 슬롯 번호 1~9를 입력하거나 슬롯의 이름/아이콘을 클릭하면 그 슬롯에 배치할 캐릭터 목록으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.CharacterSelect, Arg: v.Slot.ToString())))
                        .Concat(PartySelectEntries(Screen.PartySetup))
                        .Concat(ModeEntries(Screen.PartySetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.EquipmentSetup:
                    // 캐릭터가 있는 슬롯만 고를 수 있다. 이 화면에서 돌아가면 파티 설정이 아니라 홈으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.EquipmentList, Arg: v.Slot.ToString())))
                        .Concat(PartySelectEntries(Screen.EquipmentSetup))
                        .Concat(ModeEntries(Screen.EquipmentSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.LeaderSetup:
                    // 리더 태그를 가진 캐릭터의 슬롯만 고를 수 있다(고르면 리더로 지정하거나 해제). 돌아가면 홈으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.LeaderSetup, EntryAction.ToggleLeader, v.Slot.ToString(),
                            Enabled: HasLeaderTag(v.InstanceId!))))
                        .Concat(PartySelectEntries(Screen.LeaderSetup))
                        .Concat(ModeEntries(Screen.LeaderSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.SkillSetup:
                    // 캐릭터가 있는 슬롯만 고를 수 있고 고르면 그 캐릭터의 스킬 설정으로 간다. 돌아가면 홈으로 간다.
                    return RenamePartyEntries()
                        .Concat(PartySlotViews
                        .Where(v => !v.IsEmpty)
                        .Select(v => new ScreenEntry(
                            (v.Slot + 1).ToString(), v.Name, Screen.SkillCharacter, Arg: v.Slot.ToString())))
                        .Concat(PartySelectEntries(Screen.SkillSetup))
                        .Concat(ModeEntries(Screen.SkillSetup))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.PartyRename:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", partyRenameReturn)];
                case Screen.EquipmentList:
                    // 그 캐릭터가 가진 장비 슬롯(슬롯 코드를 입력하거나 클릭하면 그 슬롯에 낄 수 있는 장비 목록으로 간다).
                    return CurrentEquipmentSlots
                        .Select(s => new ScreenEntry(Text(s.Code), EquipmentSlotLabel(s), Screen.EquipmentSelect))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.EquipmentSetup))
                        .ToList();
                case Screen.EquipmentSelect:
                    return EquipmentSelectEntries();
                case Screen.CharacterSelect:
                    // 맨 위에 "[0] 비우기"(이 슬롯에 캐릭터가 있을 때만), 그다음 고를 수 있는 캐릭터(회색 제외)를 줄 번호로 입력하거나 클릭하면 배치한다(항목의 키는 인스턴스 ID).
                    return ClearEntries()
                        .Concat(CharacterList
                            .Where(c => c.Selectable)
                            .Select(c => new ScreenEntry(c.InstanceId, c.Name, Screen.PartySetup, EntryAction.PlaceCharacter)))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.PartySetup))
                        .ToList();
                case Screen.SupportSelect:
                    return SupportSelectEntries();
                case Screen.DungeonSelect:
                    return DungeonSelectEntries();
                case Screen.DungeonPartySelect:
                    return DungeonPartySelectEntries();
                case Screen.DungeonMove:
                    return DungeonMoveEntries();
                case Screen.GameOver:
                    return [new ScreenEntry(BackEntryCode, "로드 화면으로", Screen.Load)];
                case Screen.Hideout:
                    return HideoutEntries();
                case Screen.HideoutExpand:
                    return HideoutExpandEntries();
                case Screen.FacilityCheck:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout)];
                case Screen.HideoutFloorDetail:
                    return [.. CurrentSlot is { } floorAt ? HideoutOperatorEntries(floorAt, false, 1) : [], new ScreenEntry(BackEntryCode, "돌아가기", Screen.Hideout)];
                case Screen.HideoutOperatorSelect:
                    return HideoutOperatorSelectEntries();
                case Screen.HideoutFacilityDetail:
                    return HideoutFacilityDetailEntries();
                case Screen.HideoutFacilitySelect:
                    return HideoutFacilitySelectEntries();
                case Screen.HideoutBuild:
                    return HideoutBuildEntries();
                case Screen.EquipmentEnhance:
                    return EquipmentEnhanceEntries();
                case Screen.EquipmentEnhanceTarget:
                    return ModSlotEntries();
                case Screen.EquipmentModSlot:
                    return ModItemEntries();
                case Screen.Event:
                    // 조건을 만족하는 이벤트를 코드로 입력하거나 클릭하면 시작한다.
                    return AvailableEvents
                        .Select(e => new ScreenEntry(Text(e.Code), e.Name, Screen.EventPlay, EntryAction.StartEvent, Enabled: CanStartEvent))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.EventPlay:
                case Screen.CommunicationPlay:
                    return EventPlayEntries();
                case Screen.Defense:
                    return DefenseEntries();
                case Screen.Interaction:
                    // 교류 그룹을 코드(그룹 코드×100)로 입력하거나 클릭해 고른다. 고른 그룹의 항목은 선택할 수 없다.
                    // 그다음 고른 그룹의 교류들(코드순)이 교류 코드로 나오고, 고르면 그 교류의 흐름을 재생한다.
                    return CommunicationGroups
                        .Select(g => new ScreenEntry(Text(g.Code * CommunicationGroupEntryFactor), g.Name, Screen.Interaction,
                            EntryAction.SelectCommunicationGroup, Enabled: g.Code != CurrentCommunicationGroupCode))
                        .Concat(CurrentGroupCommunications
                            .Select(c => new ScreenEntry(Text(c.Code), c.Name, Screen.CommunicationPlay, EntryAction.StartCommunication)))
                        .Append(new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home))
                        .ToList();
                case Screen.Load:
                    return gameOver ? [] : [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Home)];      // 게임 오버 뒤의 로드 화면은 돌아갈 수 없다
                case Screen.StatusCheck:
                    return StatusCheckEntries();
                case Screen.CompanionCheck:
                    return CompanionCheckEntries();
                case Screen.EquipmentCheck:
                    return EquipmentCheckEntries();
                case Screen.ItemCheck:
                    return ItemCheckEntries();
                case Screen.CharacterDetail:
                case Screen.EquipmentDetail:
                case Screen.ItemDetail:
                case Screen.SkillDetail:
                    return DetailEntries();      // 던전에서 연 확인 화면은 던전으로 돌아간다
                case Screen.Battle:
                    return BattleEntries();
                case Screen.UnitStatus:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.Battle)];
                case Screen.SkillCharacter:
                    return SkillCharacterEntries();
                case Screen.SkillSlotSelect:
                    return SkillSlotSelectEntries();
                case Screen.SkillPoolList:
                    return [new ScreenEntry(BackEntryCode, "돌아가기", Screen.SkillCharacter)];
                case Screen.Debug:
                case Screen.DebugAddCharacter:
                case Screen.DebugAddEquipment:
                case Screen.DebugAddItem:
                case Screen.DebugSkillCharacter:
                case Screen.DebugAddSkill:
                    return DebugEntries();
                default:
                    return [];
            }
        }
    }

    /// <summary>
    /// 임베디드 이펙트를 보유 캐릭터(그 코드의 첫 인스턴스)에게 실행한다. 이펙트가 바꾼 상태는 세이브가 받아들이는 범위로 보정해 저장한다.
    /// 이펙트 코드나 캐릭터를 찾을 수 없으면(보유하지 않았거나 마스터에 없음) 아무것도 하지 않고 false.
    /// </summary>
    public bool ApplyEffect(long effectCode, long targetCharacterCode, long value1 = 0, long value2 = 0, long value3 = 0) =>
        InstanceIdOf(targetCharacterCode) is { } id && ApplyEffect(effectCode, id, value1, value2, value3);

    /// <summary>임베디드 이펙트를 이 캐릭터 인스턴스에게 실행한다.</summary>
    public bool ApplyEffect(long effectCode, string instanceId, long value1 = 0, long value2 = 0, long value3 = 0)
    {
        if (!Characters.TryGetValue(instanceId, out var state)
            || Pack.FindCharacter(state.CharacterCode) is not { } entry
            || !Pack.EmbeddedEffects.Contains(effectCode))
            return false;

        // 타겟과 밸류를 넘겨 이펙트를 만들고(저장), 파라미터 없이 실행한다.
        var target = new Character(entry, state);
        var effect = Pack.EmbeddedEffects.Create(effectCode, target, value1, value2, value3)!;
        effect.Execute();
        Characters[instanceId] = target.State.Clamped() with { InstanceId = instanceId };
        return true;
    }

    /// <summary>직전 Activate의 결과 안내(교환 성공/실패 등). 없으면 null. Activate마다 지워진다.</summary>
    public string? Message { get; private set; }

    /// <summary>
    /// 코드(공백 무시)에 해당하는 항목을 수행하고 그 항목을 돌려준다. 없으면 null.
    /// 이동 항목은 화면을 바꾸고, 교환 항목은 인벤토리를 바꾸며, 세이브 항목은 호출한 쪽(UI)이 파일로 내보낸다.
    /// </summary>
    public ScreenEntry? Activate(string code, bool typed = true)
    {
        Message = null;
        if (Screen == Screen.PartyRename && code.Trim() != BackEntryCode) return RenamePartyTo(code);   // 쓴 글이 새 이름이다
        code = Normalize(code);
        var entry = (typed ? PagerEntryFor(code) : null) ?? (IsPagedScreen(Screen) ? Entries : RawEntries).Concat(HomeSlotEntries).FirstOrDefault(e => e.Code == code) ?? PagerEntryFor(code);
        if (entry is { Enabled: false }) return entry;       // 회색 항목은 눌러도 아무 반응이 없다
        if (entry is { Code: BackEntryCode, Target: Screen.Home } && hideoutEditFloor is not null) entry = entry with { Target = Screen.Hideout };       // 방어 파티에서 돌아가면 아지트
        if (entry?.Target is Screen.Hideout or Screen.Home) hideoutEditFloor = null;
        RecordCommunicationCommand(entry);
        switch (entry?.Action)
        {
            case EntryAction.Navigate:
                CurrentShopCode = entry.Target == Screen.ShopTrades ? CodeOf(entry) : null;
                int? operatorFloor = null;
                if (entry.Target == Screen.HideoutOperatorSelect && entry.Arg is not null)         // "층:지은 시설인가:슬롯 코드"
                {
                    var parts = entry.Arg.Split(':');
                    operatorFloor = int.Parse(parts[0], CultureInfo.InvariantCulture);
                    hideoutOperatorBuilt = parts[1] == "1";
                    hideoutOperatorSlot = long.Parse(parts[2], CultureInfo.InvariantCulture);
                }
                CurrentSlot = entry.Target switch
                {
                    Screen.CharacterSelect or Screen.EquipmentList or Screen.HideoutFloorDetail or Screen.HideoutFacilityDetail => int.Parse(entry.Arg!, CultureInfo.InvariantCulture),
                    Screen.HideoutFacilitySelect when entry.Arg is not null => int.Parse(entry.Arg, CultureInfo.InvariantCulture),
                    Screen.HideoutFacilitySelect or Screen.HideoutBuild => CurrentSlot,                  // 같은 층을 계속 본다
                    Screen.HideoutOperatorSelect => operatorFloor,
                    Screen.SkillCharacter when entry.Arg is not null => int.Parse(entry.Arg, CultureInfo.InvariantCulture),
                    Screen.EquipmentSelect or Screen.SkillCharacter or Screen.SkillSlotSelect or Screen.SkillPoolList => CurrentSlot,      // 같은 캐릭터를 계속 본다
                    _ => null,
                };
                CurrentEquipmentSlotCode = entry.Target == Screen.EquipmentSelect ? CodeOf(entry) : null;
                CurrentSkillSlotCode = entry.Target == Screen.SkillSlotSelect ? CodeOf(entry) : null;
                CurrentSupportRole = entry.Target == Screen.SupportSelect ? Enum.Parse<SupportRole>(entry.Arg!) : null;
                if (entry.Target == Screen.Hideout && Screen == Screen.Home) hideoutViewTop = -1;      // 홈에서 들어오면 맨 위부터 보인다
                if (entry.Target == Screen.Interaction && Screen != Screen.CommunicationPlay)
                {
                    CurrentCommunicationGroupCode = CommunicationGroups.FirstOrDefault()?.Code;   // 들어가면 가장 작은 그룹이 선택되고
                    communicationHistory.Clear();                                                  // 이력은 비어 있다
                }
                if (entry.Target == Screen.EquipmentEnhance && Screen is not (Screen.EquipmentEnhance or Screen.EquipmentEnhanceTarget or Screen.EquipmentModSlot)) { EnhanceMode = null; ResetSynthesis(); }   // 강화 화면에 새로 들어오면 아무것도 골라 두지 않은 상태다
                if (entry.Target != Screen || IsDebugAddScreen(entry.Target)) ListPage = 0;       // 목록 화면에 들어오면 첫 페이지다
                Screen = entry.Target;
                break;
            case EntryAction.SelectEnhanceMode:
                EnhanceMode = Enum.Parse<EnhanceMode>(entry.Arg!);
                ResetSynthesis();
                break;
            case EntryAction.SelectSacrifice:
                synthesisSacrifices.Add(entry.Arg!);
                break;
            case EntryAction.ExecuteSynthesis:
                Message = ExecuteSynthesis();
                break;
            case EntryAction.ExecuteSmelt:
                Message = ExecuteSmelt();
                break;
            case EntryAction.CancelSynthesis:
                ResetSynthesis();
                break;
            case EntryAction.EndTurn:
                if (AdvanceTurn())
                {
                    Message = $"턴 종료: {CurrentDateText}";
                    if (BeginDefenseScreen()) break;                                                   // 적이 침입했으면 방어전 화면이 끝난 뒤 시설 서킷을 돈다
                    if (RunFacilityCircuits() is { } production) Message += $" / {production}";       // 턴 종료 뒤 시설 서킷
                }
                break;
            case EntryAction.SelectModSlot:
                ModSlotCode = long.Parse(entry.Arg!, CultureInfo.InvariantCulture);
                Screen = entry.Target;
                break;
            case EntryAction.RemoveMod:
                Message = RemoveMod();
                Screen = entry.Target;
                break;
            case EntryAction.InstallMod:
                Message = InstallMod(long.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                Screen = entry.Target;
                break;
            case EntryAction.PickEnhanceEquipment:
                EnhanceTargetId = entry.Arg;
                Screen = entry.Target;
                break;
            case EntryAction.OpenDetail:
                OpenDetail(entry.Arg!, entry.Target);
                break;
            case EntryAction.CloseDetail:
                CloseDetail();
                break;
            case EntryAction.SelectCommunicationGroup:
                CurrentCommunicationGroupCode = CodeOf(entry) / CommunicationGroupEntryFactor;
                break;
            case EntryAction.StartCommunication:
                BeginCommunication(CodeOf(entry));
                break;
            case EntryAction.SetSupport:
                SetSupportInstance(CurrentSupportRole!.Value, InstanceKeyOf(entry));
                CurrentSupportRole = null;
                Screen = entry.Target;
                break;
            case EntryAction.SetHideoutOperator:
                SetHideoutOperatorInstance(CurrentSlot!.Value, hideoutOperatorBuilt, hideoutOperatorSlot, InstanceKeyOf(entry));
                Screen = entry.Target;
                break;
            case EntryAction.ClearHideoutOperator:
                SetHideoutOperatorInstance(CurrentSlot!.Value, hideoutOperatorBuilt, hideoutOperatorSlot, null);
                Screen = entry.Target;
                break;
            case EntryAction.SelectFacility:
                selectedFacilityCode = CodeOf(entry);
                if (Pack.FacilityBuildGroupsOf(CodeOf(entry)).Count == 0)         // 재료가 필요 없는 시설은 바로 짓는다
                {
                    if (BuildFacility(CurrentSlot!.Value, CodeOf(entry))) Message = $"시설을 지었습니다: {Pack.FindFacility(CodeOf(entry))?.Name}";
                    selectedFacilityCode = null;
                    CurrentSlot = null;
                    Screen = Screen.Hideout;
                }
                else Screen = entry.Target;
                break;
            case EntryAction.BuildFacility:
                if (selectedFacilityCode is { } building && BuildFacility(CurrentSlot!.Value, building, CodeOf(entry))) Message = $"시설을 지었습니다: {Pack.FindFacility(building)?.Name}";
                selectedFacilityCode = null;
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.DemolishFacility:
                var demolished = BuiltFacilityOfFloor(CurrentSlot!.Value)?.Name;
                if (DemolishFacility(CurrentSlot!.Value)) Message = $"시설을 철거했습니다: {demolished}";
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.HideoutScroll:
                hideoutViewTop = Math.Clamp(hideoutViewTop + int.Parse(entry.Arg!, CultureInfo.InvariantCulture), -1, HideoutViewTopMax);
                break;
            case EntryAction.ExpandHideout:
                if (ExpandHideout(CodeOf(entry))) Message = $"아지트를 확장했습니다: {HideoutFloorCount}층";
                Screen = entry.Target;
                break;
            case EntryAction.EditHideoutParty:
                hideoutEditFloor = int.Parse(entry.Arg!, CultureInfo.InvariantCulture);
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.ClearSupport:
                SetSupportInstance(CurrentSupportRole!.Value, null);
                CurrentSupportRole = null;
                Screen = entry.Target;
                break;
            case EntryAction.Trade:
                Message = ExecuteTrade(CodeOf(entry));
                break;
            case EntryAction.PlaceCharacter:
                if (InstanceKeyOf(entry) is { } placed) PlaceCharacter(placed);
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.RenameParty:
                partyRenameReturn = Screen;
                Screen = entry.Target;
                break;
            case EntryAction.SelectDungeon:
                selectedDungeonCode = CodeOf(entry);
                Screen = entry.Target;
                break;
            case EntryAction.EnterDungeon:
                EnterDungeon(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.SelectParty:
                CurrentPartyIndex = int.Parse(entry.Arg!, CultureInfo.InvariantCulture);
                Screen = entry.Target;
                break;
            case EntryAction.ClearSlot:
                Party[CurrentSlot!.Value] = null;
                CurrentSlot = null;
                Screen = entry.Target;
                break;
            case EntryAction.EquipSkill:
                Message = EquipSkillFromScreen(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                CurrentSkillSlotCode = null;
                Screen = entry.Target;                  // 같은 캐릭터의 스킬 슬롯 목록(CurrentSlot 유지)
                break;
            case EntryAction.ClearSkillSlot:
                Message = ClearSkillSlotFromScreen();
                CurrentSkillSlotCode = null;
                Screen = entry.Target;
                break;
            case EntryAction.DebugAdd:
                Message = DebugAddOne(long.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.DebugSelectSkillCharacter:
                DebugSkillTarget = InstanceKeyOf(entry.Arg!);
                ListPage = 0;
                Screen = entry.Target;
                break;
            case EntryAction.ListPage:
                ListPage = int.Parse(entry.Arg!, CultureInfo.InvariantCulture) - 1;
                break;
            case EntryAction.ToggleLeader:
                ToggleLeader(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                Screen = entry.Target;
                break;
            case EntryAction.EquipItem:
                Message = EquipFromScreen(ParseRef(entry.Arg!));
                CurrentEquipmentSlotCode = null;
                Screen = entry.Target;                  // 같은 캐릭터의 장비 슬롯 목록(CurrentSlot 유지)
                break;
            case EntryAction.Unequip:
                Message = UnequipFromScreen();
                CurrentEquipmentSlotCode = null;
                Screen = entry.Target;
                break;
            case EntryAction.EndBattle:
                if (battlePartyBefore is { } before) CurrentPartyIndex = before;
                battlePartyBefore = null;
                if (battleResultFlag != 0)
                    PlayerFlagOps.Set(PlayerFlags, battleResultFlag, BattleOutcome switch { BattleOutcomeKind.Win => 1, BattleOutcomeKind.Run => 2, _ => 3 });
                battleResultFlag = 0;
                Screen = entry.Target;
                ApplyPendingCaptures();                                                  // 던전 전투에서 진 파티원 가운데 서킷이 구금하라고 한 캐릭터를 던전에 가둔다
                if (CurrentDungeonCode is not null && CurrentEvent is null && BattleOutcome == BattleOutcomeKind.Lose) EndDungeonByDefeat();      // 던전 전투 패배: 주인공이 있으면 게임 오버, 없으면 홈
                else if (CurrentEvent is not null && Screen == Screen.EventPlay) ShowNextEventStep();      // 이벤트의 Battle 칸 다음부터 이어 간다
                else if (CurrentDungeonCode is not null && Screen == Screen.DungeonMove) ContinueTileEffects();      // 던전: 남은 타일 효과를 이어 간다
                break;
            case EntryAction.BattleOpenMove:
                OpenBattleMove();
                break;
            case EntryAction.BattleMoveTo:
                ChooseBattleMove(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.BattleOpenItems:
                OpenBattleItemCandidates();
                break;
            case EntryAction.BattleChooseItem:
                ChooseBattleItem(entry.Arg!);
                break;
            case EntryAction.BattleSelectCharacter:
                SelectBattleCharacter(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.BattleOpenActions:
                OpenBattleActionCandidates();
                break;
            case EntryAction.BattleChooseSkill:
                ChooseBattleSkill(long.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.BattleExecute:
                ExecuteBattleTurn();
                break;
            case EntryAction.BattleViewUnit:
                ViewBattleUnit(entry.Arg!);
                Screen = Screen.UnitStatus;
                break;
            case EntryAction.BattleRun:
                RunFromBattle();
                break;
            case EntryAction.BattleChooseTarget:
                ChooseBattleTarget(entry.Arg!);
                break;
            case EntryAction.BattleCancelActions:
                battlePendingSkill = null;
                battlePendingItem = null;
                BattleCommands = BattleCommandMode.Basic;
                break;
            case EntryAction.BattleScroll:
                ScrollBattle(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
            case EntryAction.StartEvent:
                BeginEvent(CodeOf(entry));
                break;
            case EntryAction.NextEventStep:
                ContinueEvent();
                break;
            case EntryAction.NextDefenseStep:
                NextDefenseStep();
                break;
            case EntryAction.ChooseAdv:
                ChooseAdv(int.Parse(entry.Arg!, CultureInfo.InvariantCulture));
                break;
        }
        return entry;
    }

    /// <summary>현재 선택한 슬롯에 캐릭터가 있으면 "[0] 비우기" 항목 하나, 없으면 없음.</summary>
    IEnumerable<ScreenEntry> ClearEntries() =>
        CurrentSlot is { } slot && PartyInstance.Units[slot] is { } unit && !IsLastLeaderWithCompany(unit)
            ? [new ScreenEntry(ClearEntryCode, "비우기", Screen.PartySetup, EntryAction.ClearSlot)]
            : [];

    /// <summary>다른 유닛이 남아 있는데 이 유닛이 마지막 리더인가(비우면 파티에 리더가 없어지므로 비울 수 없다).</summary>
    bool IsLastLeaderWithCompany(PartyUnit unit) =>
        unit.IsLeader && LeaderCount(PartyInstance) <= 1 && PartyInstance.Units.Count(u => u is not null) > 1;

    /// <summary>
    /// 파티 설정 화면의 파티 선택 버튼 열여섯 개: [11] 파티-A ~ [26] 파티-P. 지금 보는 파티의 버튼은 선택할 수 없다(파란색).
    /// 고르면 그 파티를 보고(같은 화면에 머문다) 슬롯과 캐릭터 배치, 장비 착용은 그 파티를 따른다.
    /// </summary>
    /// <summary>파티 설정 화면 제목의 "[0] 파티명" 항목 하나: 누르면 파티 이름 변경 화면으로 간다.</summary>
    IEnumerable<ScreenEntry> RenamePartyEntries() =>
        hideoutEditFloor is not null ? [] : [new ScreenEntry(RenamePartyEntryCode, PartyName, Screen.PartyRename, EntryAction.RenameParty)];       // 방어 파티의 이름(층 이름)은 바꿀 수 없다

    /// <summary>
    /// 파티 이름 변경 화면에서 입력칸에 쓴 글을 새 이름으로 받는다: 앞뒤 공백을 없앤 글이 비어 있으면 그대로 두고,
    /// <see cref="MaxPartyNameLength"/>자를 넘으면 안내만 하고, 아니면 이름을 바꿔 들어온 화면으로 돌아간다.
    /// </summary>
    ScreenEntry? RenamePartyTo(string typed)
    {
        var name = typed.Trim();
        if (name.Length == 0) return null;
        if (name.Length > MaxPartyNameLength)
        {
            Message = $"파티 이름은 {MaxPartyNameLength}자까지입니다.";
            return null;
        }
        PartyName = name;
        Screen = partyRenameReturn;
        return new ScreenEntry(RenamePartyEntryCode, name, Screen, EntryAction.RenameParty);
    }

    IEnumerable<ScreenEntry> PartySelectEntries(Screen screen) =>
        hideoutEditFloor is not null ? [] : Parties.Select((p, i) => new ScreenEntry(
            (PartySelectCodeBase + i).ToString(CultureInfo.InvariantCulture), p.Name, screen, EntryAction.SelectParty,
            i.ToString(CultureInfo.InvariantCulture), Enabled: i != CurrentPartyIndex));

    /// <summary>파티 설정 화면 아래의 모드 버튼들: [101] 캐릭터 배치, [102] 장비 착용, [103] 스킬 설정, [104] 리더 지정. 지금 보고 있는 모드의 버튼은 선택할 수 없다.</summary>
    static IEnumerable<ScreenEntry> ModeEntries(Screen current) =>
    [
        new ScreenEntry(PlacementModeCode, "캐릭터 배치", Screen.PartySetup, Enabled: current != Screen.PartySetup),
        new ScreenEntry(EquipmentModeCode, "장비 착용", Screen.EquipmentSetup, Enabled: current != Screen.EquipmentSetup),
        new ScreenEntry(SkillModeCode, "스킬 설정", Screen.SkillSetup, Enabled: current != Screen.SkillSetup),
        new ScreenEntry(LeaderModeCode, "리더 지정", Screen.LeaderSetup, Enabled: current != Screen.LeaderSetup),
    ];

    static string Text(long code) => code.ToString(CultureInfo.InvariantCulture);

    static long CodeOf(ScreenEntry entry) => long.Parse(entry.DataCode ?? entry.Code, CultureInfo.InvariantCulture);

    /// <summary>캐릭터 목록 항목이 가리키는 인스턴스 ID(항목의 키). 키가 캐릭터 코드(정수)면 그 코드의 첫 보유 인스턴스(있다면 아직 배치하지 않은 것)로 푼다. 보유하지 않으면 null.</summary>
    string? InstanceKeyOf(ScreenEntry entry) => InstanceKeyOf(entry.DataCode ?? entry.Code);

    string? InstanceKeyOf(string key) =>
        Characters.ContainsKey(key) ? key
        : long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? PlaceableInstanceOf(code) : null;

    /// <summary>입력을 다듬는다. 앞뒤 공백을 없애고, 숫자면 앞자리 0을 없앤다("007001" → "7001").</summary>
    static string Normalize(string typed)
    {
        typed = typed.Trim();
        return long.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? Text(n) : typed;
    }

    /// <summary>
    /// 선택한 슬롯에 캐릭터 인스턴스를 둔다. 이미 같은 파티의 다른 슬롯에 있으면 그 슬롯과 이 슬롯의 유닛을 맞바꾸고(빈 슬롯이면 이동; 장비는 유닛과 함께 간다),
    /// 어디에도 없으면 이 슬롯에 장비 없는 새 유닛으로 두며 원래 있던 유닛은 장비와 함께 파티에서 빠진다(장비는 보유로 돌아간다).
    /// 이 슬롯의 캐릭터를 다시 고르면 그대로다.
    /// </summary>
    void PlaceCharacter(string instanceId)
    {
        if (!Characters.TryGetValue(instanceId, out var character) || IsCapturedEnemy(instanceId) || IsCapturedAlly(instanceId)) return;
        var slot = CurrentSlot!.Value;
        var units = PartyInstance.Units;
        var at = IndexOfId(PartyInstance, instanceId);
        if (at == slot) return;
        if (at >= 0) (units[at], units[slot]) = (units[slot], units[at]);
        else
        {
            RemoveFromHideout(instanceId);                           // 파티에 넣으면 아지트에서 빠진다
            units[slot] = new PartyUnit(character.CharacterCode, [], IsLeader: IsLeaderOnlySlot(slot), InstanceId: instanceId);     // 아무도 없던 파티의 첫 유닛이나 리더 자리를 이은 유닛은 리더다
        }
    }

    /// <summary>
    /// 이 슬롯에 두는 캐릭터가 리더 태그를 가져야 하는가: 지금 보는 파티에 아무도 없거나 이 슬롯이 리더의 슬롯일 때.
    /// 이때 새로 두는 유닛은 리더가 된다.
    /// </summary>
    bool IsLeaderOnlySlot(int slot) =>
        PartyInstance.Units.All(u => u is null) || PartyInstance.Units[slot]?.IsLeader == true;

    /// <summary>이 캐릭터가 리더 태그(<see cref="GamePack.LeaderTagCode"/>)를 가졌는가(캐릭터 코드의 첫 인스턴스의 지금 태그 기준).</summary>
    public bool HasLeaderTag(long characterCode) => CharacterTags(characterCode).Contains(Pack.LeaderTagCode);

    /// <summary>이 캐릭터 인스턴스가 리더 태그를 가졌는가.</summary>
    public bool HasLeaderTag(string instanceId) => CharacterTags(instanceId).Contains(Pack.LeaderTagCode);

    /// <summary>이 파티의 리더 수.</summary>
    static int LeaderCount(PartyInstance party) => party.Units.Count(u => u is { IsLeader: true });

    /// <summary>
    /// 이 파티를 던전(전투)에 파견할 수 있는가: 리더가 한 명 이상 있어야 한다. 어떤 이유로 리더가 없는 파티(빈 파티, 리더가 리더 태그를 잃음,
    /// 리더가 파티에서 빠짐)는 다른 유닛이 있어도 파견할 수 없고, 다른 유닛이 리더가 되지도 않는다(리더 지정에서 다시 지정해야 한다).
    /// </summary>
    public bool IsDispatchable(int partyIndex) => LeaderCount(Parties[partyIndex]) > 0;

    /// <summary>리더가 리더 태그를 잃으면 리더에서 빠진다(리더 태그가 없는 리더는 없다). 다른 유닛이 대신 리더가 되지는 않는다.</summary>
    void DropLeadersWithoutTag()
    {
        foreach (var party in AllParties)
            for (var i = 0; i < party.Units.Length; i++)
                if (party.Units[i] is { IsLeader: true } leader && !(IdOf(leader) is { } leaderId && HasLeaderTag(leaderId)))
                    party.Units[i] = leader with { IsLeader = false };
    }

    /// <summary>
    /// 이 슬롯의 유닛을 리더로 지정하거나(이미 리더면) 해제한다. 리더 태그가 없으면 할 수 없고, 마지막 리더는 해제할 수 없다
    /// (파티에는 리더가 한 명 이상 있어야 한다). 한 파티의 리더는 한 명이라, 다른 유닛을 리더로 지정하면 원래 리더는 리더에서 해제된다.
    /// </summary>
    void ToggleLeader(int slot)
    {
        if (PartyInstance.Units[slot] is not { } unit || IdOf(unit) is not { } unitId || !HasLeaderTag(unitId)) return;
        if (unit.IsLeader && LeaderCount(PartyInstance) <= 1)
        {
            Message = "파티에는 리더가 한 명 이상 있어야 합니다.";
            return;
        }
        for (var i = 0; i < PartyInstance.Units.Length; i++)
            if (i != slot && PartyInstance.Units[i] is { IsLeader: true } other)
                PartyInstance.Units[i] = other with { IsLeader = false };      // 리더는 파티에 한 명이다
        PartyInstance.Units[slot] = unit with { IsLeader = !unit.IsLeader };
    }

    /// <summary>
    /// 코스트를 인벤토리에서 내고 리시브를 받는다. 번치 아이템은 부품으로 풀어서 처리한다.
    /// 코스트가 모자라거나 소지제한을 넘거나 수량이 long 범위를 넘으면 아무것도 바꾸지 않고 사유를 돌려준다.
    /// </summary>
    string ExecuteTrade(long tradeCode)
    {
        var trade = Pack.Trades!.First(t => t.Code == tradeCode);
        var text = new TradeLine(
            trade.Code, Pack.TradeTargetName(trade.CostKind, trade.CostItemCode), trade.CostAmount,
            Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode), trade.ReceiveAmount).Text;

        try
        {
            return ExecuteTrade(trade, text);
        }
        catch (OverflowException)
        {
            return $"교환 실패 - 수량이 너무 커서 계산할 수 없습니다. [{text}]";
        }
    }

    string? PlanTrade(ShopTrade trade, string text, out TradePlan plan)
    {
        plan = default;
        // 커먼 아이템은 번치를 부품으로 풀어 한꺼번에 계산한다. 유니크 아이템과 캐릭터는 확인한 뒤 따로 처리한다.
        var net = new Dictionary<long, long>();
        string[] paidInstances = [];
        string? paidCharacter = null;

        // 코스트는 받기 전의 보유량으로 판단한다.
        switch (trade.CostKind)
        {
            case TradeKind.CommonItem:
                foreach (var (item, need) in Pack.Expand(trade.CostItemCode, trade.CostAmount))
                {
                    // 장비 슬롯에 끼워 둔 커먼 아이템은 쓸 수 없다(끼운 개수가 보유 개수를 넘지 않게 한다).
                    var have = InventoryItems.GetValueOrDefault(item).Amount - EquippedCount(EquipRef.Common(item));
                    if (have < need) return $"교환 실패 - 부족: {Pack.ItemName(item)} (보유 {have} / 필요 {need}) [{text}]";
                    // 합산은 checked라 long 범위를 넘으면 OverflowException이 난다(바깥에서 실패로 처리).
                    net[item] = checked(net.GetValueOrDefault(item) - need);
                }
                break;
            case TradeKind.UniqueItem:
                // 장비 슬롯에 끼지 않은 인스턴스만 낼 수 있다(작은 ID부터).
                var spare = InstancesOf(trade.CostItemCode).Where(id => EquippedCount(EquipRef.Unique(id)) == 0).ToList();
                if (spare.Count < trade.CostAmount)
                    return $"교환 실패 - 부족: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} (보유 {spare.Count} / 필요 {trade.CostAmount}) [{text}]";
                paidInstances = spare.Take((int)trade.CostAmount).ToArray();
                break;
            case TradeKind.Character:
                if (trade.CostItemCode == GamePack.PlayerCharacterCode)
                    return $"교환 실패 - 플레이어는 낼 수 없습니다: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} [{text}]";
                var owned = Characters.InstancesOf(trade.CostItemCode);
                if (owned.Count == 0)
                    return $"교환 실패 - 부족: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} (보유 0 / 필요 1) [{text}]";
                // 같은 캐릭터가 여럿이면 그중 파티에 편성되지 않고 장비로 끼워지지 않은 인스턴스를 낸다(아지트에 선 것보다 놀고 있는 것이 먼저).
                var spareCharacter = owned.Where(c => PartyIndexOf(c.InstanceId) < 0 && HideoutFloorOf(c.InstanceId) < 0 && EquippedCount(EquipRef.Char(c.InstanceId)) == 0).Select(c => c.InstanceId)
                    .Concat(owned.Where(c => PartyIndexOf(c.InstanceId) < 0 && EquippedCount(EquipRef.Char(c.InstanceId)) == 0).Select(c => c.InstanceId)).FirstOrDefault();
                if (spareCharacter is null)
                    return owned.Any(c => PartyIndexOf(c.InstanceId) >= 0)
                        ? $"교환 실패 - 파티에 편성된 캐릭터: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} [{text}]"
                        : $"교환 실패 - 장비 슬롯에 끼워 둔 캐릭터: {Pack.TradeTargetName(trade.CostKind, trade.CostItemCode)} [{text}]";
                paidCharacter = spareCharacter;
                break;
        }

        switch (trade.ReceiveKind)
        {
            case TradeKind.CommonItem:
                foreach (var (item, n) in Pack.Expand(trade.ReceiveItemCode, trade.ReceiveAmount))
                    net[item] = checked(net.GetValueOrDefault(item) + n);
                break;
            case TradeKind.Character:
                // 같은 캐릭터를 내고 받는 교환이면 낸 뒤의 보유 수로 소지 한도를 견준다.
                var heldAfter = Characters.CountOf(trade.ReceiveItemCode) - (paidCharacter is not null && Characters[paidCharacter].CharacterCode == trade.ReceiveItemCode ? 1 : 0);
                var limit = CharacterLimitOf(trade.ReceiveItemCode);
                if (heldAfter >= limit)
                    return limit == 1
                        ? $"교환 실패 - 이미 보유한 캐릭터: {Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode)} [{text}]"
                        : $"교환 실패 - 소지 한도 초과: {Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode)} (한도 {limit}) [{text}]";
                break;
        }

        // 커먼 아이템 인벤토리 마스터의 소지제한을 넘게 되면 교환하지 않는다. 마스터에 없는 아이템은 long 한도까지.
        foreach (var (item, delta) in net)
        {
            if (delta <= 0) continue;
            var limit = Pack.FindItem(item)?.CarryLimit ?? long.MaxValue;
            if (checked(InventoryItems.GetValueOrDefault(item).Amount + delta) > limit)
                return $"교환 실패 - 소지 한도 초과: {Pack.ItemName(item)} (한도 {limit}) [{text}]";
        }

        plan = new TradePlan(net, paidInstances, paidCharacter);
        return null;
    }

    /// <summary>교환을 해 보지 않고 미리 계산한 결과: 커먼 아이템 증감, 낼 유니크 아이템 인스턴스, 낼 캐릭터.</summary>
    readonly record struct TradePlan(Dictionary<long, long> Net, string[] PaidInstances, string? PaidCharacter);

    /// <summary>
    /// 상점 교환 화면의 이 교환을 지금 할 수 있는가(코스트가 모자라거나 소지 한도를 넘거나 이미 가진 캐릭터를 받는 등이면 거짓).
    /// 인벤토리는 바꾸지 않고 확인만 한다. 마스터에 없는 교환 코드는 거짓.
    /// </summary>
    public bool CanTrade(long tradeCode) => TradeBlockedReason(tradeCode) is null;

    /// <summary>이 교환을 지금 할 수 없는 까닭("교환 실패 - 부족: …" 형식). 할 수 있으면 null. 인벤토리는 바꾸지 않는다.</summary>
    public string? TradeBlockedReason(long tradeCode)
    {
        var trade = Pack.Trades?.FirstOrDefault(t => t.Code == tradeCode);
        if (trade is null) return "교환 실패 - 없는 교환입니다.";
        var text = new TradeLine(
            trade.Code, Pack.TradeTargetName(trade.CostKind, trade.CostItemCode), trade.CostAmount,
            Pack.TradeTargetName(trade.ReceiveKind, trade.ReceiveItemCode), trade.ReceiveAmount).Text;
        try
        {
            return PlanTrade(trade, text, out _);
        }
        catch (OverflowException)
        {
            return $"교환 실패 - 수량이 너무 커서 계산할 수 없습니다. [{text}]";
        }
    }

    string ExecuteTrade(ShopTrade trade, string text)
    {
        if (PlanTrade(trade, text, out var plan) is { } failure) return failure;
        var (net, paidInstances, paidCharacter) = plan;

        foreach (var (item, delta) in net)
        {
            var after = InventoryItems.GetValueOrDefault(item).Amount + delta;
            if (after == 0) InventoryItems.Remove(item);
            else InventoryItems[item] = new InventoryItem(after);
        }
        foreach (var id in paidInstances) RemoveUniqueItem(id);
        if (paidCharacter is { } leaving) RemoveCharacter(leaving);

        if (trade.ReceiveKind == TradeKind.UniqueItem)
            for (var i = 0L; i < trade.ReceiveAmount; i++) AddUniqueItem(trade.ReceiveItemCode);
        else if (trade.ReceiveKind == TradeKind.Character)
            AddCharacter(trade.ReceiveItemCode);
        return $"교환 완료: {text}";
    }

    /// <summary>
    /// 캐릭터를 내보낸다(그 코드의 인스턴스 하나): 파티나 아지트에 서지 않은 인스턴스가 먼저이고 없으면 아무거나(OrderNumber순)다. 다른 캐릭터에게 장비로 끼워진 인스턴스는 고르지 않는다.
    /// 나머지는 <see cref="RemoveCharacter(string)"/>와 같다. 보유하지 않았거나 낼 수 있는 인스턴스가 없으면 false.
    /// </summary>
    public bool RemoveCharacter(long characterCode)
    {
        if (characterCode == GamePack.PlayerCharacterCode) return false;       // 플레이어는 내보낼 수 없다
        var removable = Characters.InstancesOf(characterCode).Where(c => EquippedCount(EquipRef.Char(c.InstanceId)) == 0).ToList();
        var pick = removable.FirstOrDefault(c => !IsPlaced(c.InstanceId)) ?? removable.FirstOrDefault();
        return pick is not null && RemoveCharacter(pick.InstanceId);
    }

    /// <summary>
    /// 캐릭터 인스턴스를 내보낸다: 보유 캐릭터에서 빠지고, 파티 슬롯과 상태이상, 태그가 지워지며, 끼고 있던 장비는 빠져 보유로 돌아간다.
    /// 같은 코드의 다른 인스턴스는 그대로다. 다른 캐릭터에게 장비로 끼워져 있으면 하지 않고 false(보유하지 않아도 false).
    /// </summary>
    public bool RemoveCharacter(string instanceId)
    {
        if (!Characters.TryGetValue(instanceId, out var character) || character.CharacterCode == GamePack.PlayerCharacterCode) return false;       // 플레이어는 내보낼 수 없다
        if (EquippedCount(EquipRef.Char(instanceId)) > 0) return false;
        RemoveFromParties(instanceId);
        RemoveFromHideout(instanceId);
        if (PartnerId == instanceId) PartnerId = null;
        if (AssistId == instanceId) AssistId = null;
        Characters.Remove(instanceId);
        CharacterStatusEffects.Remove(instanceId);
        CharacterTagInstances.Remove(instanceId);
        return true;
    }

    // ---- 상태이상 ----

    /// <summary>캐릭터(그 코드의 첫 인스턴스)에 걸려 있는 상태이상들. 없으면 빈 목록.</summary>
    public IReadOnlyList<ActiveStatusEffect> StatusEffectsOf(long characterCode) =>
        InstanceIdOf(characterCode) is { } id ? StatusEffectsOf(id) : [];

    /// <summary>캐릭터 인스턴스에 걸려 있는 상태이상들. 없으면 빈 목록.</summary>
    public IReadOnlyList<ActiveStatusEffect> StatusEffectsOf(string instanceId) =>
        CharacterStatusEffects.GetValueOrDefault(instanceId) ?? [];

    /// <summary>
    /// 보유 캐릭터에게 상태이상을 건다. 상태이상 이펙트 마스터의 값(시점, 우선도, 이펙트 코드, 밸류 1~3)을 복사해 두므로
    /// 카운터 0, 유효 상태로 시작하고, 이후 게임팩 마스터가 바뀌어도 걸린 값은 그대로다.
    /// 이미 같은 상태이상이 걸려 있으면 새로 걸어 갱신한다. 캐릭터를 보유하지 않았거나 상태이상 코드가 없으면 false.
    /// </summary>
    public bool ApplyStatusEffect(long characterCode, long statusEffectCode) =>
        InstanceIdOf(characterCode) is { } id && ApplyStatusEffect(id, statusEffectCode);

    /// <summary>캐릭터 인스턴스에게 상태이상을 건다(<see cref="ApplyStatusEffect(long, long)"/>와 같다).</summary>
    public bool ApplyStatusEffect(string instanceId, long statusEffectCode)
    {
        if (!Characters.ContainsKey(instanceId)) return false;
        if (Pack.FindStatusEffect(statusEffectCode) is null) return false;

        var effects = Pack.StatusEffectEffectsOf(statusEffectCode).Select(ActiveEffect.From).ToList();
        if (!CharacterStatusEffects.TryGetValue(instanceId, out var list))
            CharacterStatusEffects[instanceId] = list = [];
        list.RemoveAll(s => s.StatusEffectCode == statusEffectCode);
        list.Add(new ActiveStatusEffect(statusEffectCode, effects));
        return true;
    }

    /// <summary>걸려 있는 상태이상을 푼다. 걸려 있지 않았으면 false.</summary>
    public bool RemoveStatusEffect(long characterCode, long statusEffectCode) =>
        InstanceIdOf(characterCode) is { } id && RemoveStatusEffect(id, statusEffectCode);

    /// <summary>캐릭터 인스턴스에서 걸려 있는 상태이상을 푼다.</summary>
    public bool RemoveStatusEffect(string instanceId, long statusEffectCode)
    {
        if (!CharacterStatusEffects.TryGetValue(instanceId, out var list)) return false;
        var removed = list.RemoveAll(s => s.StatusEffectCode == statusEffectCode) > 0;
        if (list.Count == 0) CharacterStatusEffects.Remove(instanceId);
        return removed;
    }

    /// <summary>저장용 복사본(이펙트 목록도 새로 만든다). 비어 있는 캐릭터는 뺀다.</summary>
    static Dictionary<string, List<ActiveStatusEffect>> CopyStatusEffects(Dictionary<string, List<ActiveStatusEffect>> source) =>
        source.Where(kv => kv.Value.Count > 0).ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(s => new ActiveStatusEffect(s.StatusEffectCode, s.EffectList.ToList())).ToList());

    /// <summary>세이브의 상태이상을 검증하고 복사한다. 보유 캐릭터, 상태이상 마스터, 임베디드 이펙트에 없는 것은 오류.</summary>
    static Dictionary<string, List<ActiveStatusEffect>> ValidateStatusEffects(
        GamePack pack, Dictionary<string, CharacterInstance> characters, Dictionary<string, List<ActiveStatusEffect>>? saved, Func<string, string?> resolve)
    {
        var result = new Dictionary<string, List<ActiveStatusEffect>>();
        foreach (var (savedKey, list) in saved ?? [])
        {
            var characterCode = savedKey;
            if (resolve(savedKey) is not { } characterId || !characters.ContainsKey(characterId))
                throw new SaveFormatException($"세이브의 상태이상이 보유하지 않은 캐릭터 '{characterCode}'에 걸려 있습니다.");

            var seen = new HashSet<long>();
            var copy = new List<ActiveStatusEffect>();
            foreach (var status in list ?? [])
            {
                if (status is null) throw new SaveFormatException($"세이브의 캐릭터 '{characterCode}'의 상태이상 목록에 빈 항목이 있습니다.");
                if (pack.FindStatusEffect(status.StatusEffectCode) is null)
                    throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'가 상태이상 마스터에 없습니다.");
                if (!seen.Add(status.StatusEffectCode))
                    throw new SaveFormatException(
                        $"세이브의 캐릭터 '{characterCode}'에 상태이상 '{status.StatusEffectCode}'가 두 번 이상 있습니다.");

                foreach (var effect in status.EffectList)
                {
                    if (effect is null)
                        throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'에 빈 이펙트 항목이 있습니다.");
                    if (!Enum.IsDefined(effect.Trigger))
                        throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'의 트리거 타입이 올바르지 않습니다: {effect.Trigger}");
                    if (!pack.EmbeddedEffects.Contains(effect.EffectCode))
                        throw new SaveFormatException(
                            $"세이브의 상태이상 '{status.StatusEffectCode}'의 이펙트 코드 '{effect.EffectCode}'가 임베디드 이펙트에 없습니다.");
                    if (effect.Counter < 0)
                        throw new SaveFormatException($"세이브의 상태이상 '{status.StatusEffectCode}'의 카운터가 음수입니다: {effect.Counter}");
                }
                copy.Add(new ActiveStatusEffect(status.StatusEffectCode, status.EffectList.ToList()));
            }
            if (copy.Count > 0) result[characterId] = copy;
        }
        return result;
    }

    public string ToSaveJson() =>
        JsonSerializer.Serialize(
            new SaveData(
                SaveVersion, Pack.Id, Screen,
                new Dictionary<long, long>(PlayerFlags),
                new Dictionary<long, InventoryItem>(InventoryItems),
                new Dictionary<string, CharacterInstance>(Characters),
                null,
                CopyStatusEffects(CharacterStatusEffects),
                CopyUniqueItems(),
                CopyCharacterTags(),
                null,
                null, null, TurnCount,
                null,
                Parties.Select(p => p.Copy()).ToArray(),
                CurrentPartyIndex,
                hideoutParties.Select(p => p.Copy()).ToArray(),
                hideoutFacilities.ToArray(),
                SaveHideoutOperators(CurrentHideout),
                PartnerId, AssistId,
                DungeonCaptives.Count == 0 ? null : DungeonCaptives.ToDictionary(d => d.Key, d => d.Value.Select(u => u.Copy()).ToArray()),
                Pack.Hideouts is null ? null : CurrentHideoutCode,
                Pack.Hideouts is null || hideoutStates.Count <= 1 ? null
                    : hideoutStates.Where(h => h.Key != CurrentHideoutCode).ToDictionary(h => h.Key,
                        h => new HideoutSave(h.Value.Parties.Select(p => p.Copy()).ToArray(), h.Value.Facilities.ToArray(), SaveHideoutOperators(h.Value)))),
            SaveJsonContext.Default.SaveData);

    HideoutOperatorSave[] SaveHideoutOperators(HideoutState state) =>
        state.Operators.Select(o => new HideoutOperatorSave(o.Key.FloorOrder, o.Key.Built, o.Key.Slot, CodeOfInstance(o.Value) ?? 0, o.Value)).ToArray();

    public static GameSession FromSaveJson(GamePack pack, string json)
    {
        SaveData? save;
        try { save = JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveData); }
        catch (JsonException) { throw new SaveFormatException("세이브 파일이 올바른 JSON이 아닙니다."); }

        if (save is null) throw new SaveFormatException("세이브 파일이 비어 있습니다.");
        if (save.Version is < 1 or > SaveVersion) throw new SaveFormatException($"지원하지 않는 세이브 버전입니다: {save.Version}");
        if (save.PackId != pack.Id) throw new SaveFormatException($"다른 게임팩의 세이브입니다: {save.PackId}");
        var playerFlags = save.PlayerFlags is null ? [] : new Dictionary<long, long>(save.PlayerFlags);
        var inventory = save.InventoryItems is null ? [] : new Dictionary<long, InventoryItem>(save.InventoryItems);
        foreach (var (code, item) in inventory)
            if (item.Amount < 0) throw new SaveFormatException($"세이브의 인벤토리 아이템 {code}의 수량이 음수입니다: {item.Amount}");

        // 보유 캐릭터: 키는 인스턴스 ID(ULID)다. 키가 정수면 캐릭터 코드를 뜻하는 줄임 표기로 보고, 인스턴스 ID가 없으면 새로 채운다
        // (ID는 만든 순서와 같게 늘어난다). 같은 코드의 인스턴스는 마스터의 carry_limit개까지 있을 수 있다.
        var savedCharacters = (save.Characters ?? []).Where(kv => kv.Key is not null).ToList();
        var characters = new CharacterCollection();
        var usedIds = (save.UniqueItems?.Keys ?? Enumerable.Empty<string>())
            .Concat(savedCharacters.Select(kv => kv.Value?.InstanceId ?? "").Where(id => !string.IsNullOrEmpty(id)))
            .Concat(savedCharacters.Select(kv => kv.Key).Where(IsUlidKey)).ToList();
        foreach (var (key, s) in savedCharacters.OrderBy(kv => IsUlidKey(kv.Key) ? 1 : 0)
                     .ThenBy(kv => long.TryParse(kv.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                     .ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (s is null) throw new SaveFormatException($"세이브의 캐릭터 '{key}'가 비어 있습니다.");
            var byId = IsUlidKey(key);
            long code;
            if (byId)
            {
                code = s.CharacterCode;
                if (code == 0) throw new SaveFormatException($"세이브의 캐릭터 '{key}'에 마스터 코드가 없습니다.");
            }
            else if (long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out code))
            {
                if (s.CharacterCode != 0 && s.CharacterCode != code)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'에 적힌 마스터 코드 '{s.CharacterCode}'가 키와 다릅니다.");
            }
            else throw new SaveFormatException($"세이브의 캐릭터 키 '{key}'가 인스턴스 ID(ULID)도 캐릭터 코드도 아닙니다.");
            if (pack.FindCharacter(code) is null)
                throw new SaveFormatException($"세이브의 캐릭터 '{code}'가 캐릭터 인벤토리 마스터에 없습니다.");
            string instanceId;
            if (byId)
            {
                instanceId = Ulid.Parse(key).ToString();
                if (!string.IsNullOrEmpty(s.InstanceId) && !(Ulid.TryParse(s.InstanceId, out var inner) && inner.ToString() == instanceId))
                    throw new SaveFormatException($"세이브의 캐릭터 '{instanceId}'에 적힌 인스턴스 ID '{s.InstanceId}'가 키와 다릅니다.");
            }
            else if (string.IsNullOrEmpty(s.InstanceId))
            {
                instanceId = NextInstanceId(usedIds);
                usedIds.Add(instanceId);
            }
            else if (!Ulid.TryParse(s.InstanceId, out var ulid))
                throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 인스턴스 ID '{s.InstanceId}'가 ULID가 아닙니다.");
            else instanceId = ulid.ToString();
            if (characters.ContainsKey(instanceId))
                throw new SaveFormatException($"세이브의 캐릭터 인스턴스 ID '{instanceId}'가 두 번 이상 있습니다.");
            // 고정 필드(Level, Hp, MaxHp, Xpp, XppMax)로 저장한 예전 세이브는 Statuses로 옮긴다.
            // (JSON에 없는 항목은 null로 올 수 있다)
            var statuses = new Dictionary<long, long>((Dictionary<long, long>?)s.Statuses ?? []);
            if (s.LegacyLevel is not null || s.LegacyHp is not null || s.LegacyMaxHp is not null || s.LegacyXpp is not null || s.LegacyXppMax is not null)
            {
                statuses.TryAdd(GamePack.LevelStatusCode, s.LegacyLevel ?? 1);
                statuses.TryAdd(GamePack.HpStatusCode, s.LegacyHp ?? 0);
                statuses.TryAdd(GamePack.MaxHpStatusCode, s.LegacyMaxHp ?? 0);
                statuses.TryAdd(GamePack.XppStatusCode, s.LegacyXpp ?? 0);
                statuses.TryAdd(GamePack.XppMaxStatusCode, s.LegacyXppMax ?? 0);
            }
            var loaded = s with
            {
                InstanceId = instanceId, CharacterCode = code, Statuses = statuses,
                SkillPool = (List<SkillInstance>?)s.SkillPool ?? [], SkillSlots = (Dictionary<long, long>?)s.SkillSlots ?? [],
                LegacyLevel = null, LegacyHp = null, LegacyMaxHp = null, LegacyXpp = null, LegacyXppMax = null,
            };
            if (loaded.Level < 1 || loaded.MaxHp < 0 || loaded.Hp < 0 || loaded.Hp > loaded.MaxHp
                || loaded.XppMax < 0 || loaded.Xpp < 0 || loaded.Xpp > loaded.XppMax)
                throw new SaveFormatException(
                    $"세이브의 캐릭터 '{code}' 상태가 올바르지 않습니다 (Lv {loaded.Level}, HP {loaded.Hp}/{loaded.MaxHp}, XPP {loaded.Xpp}/{loaded.XppMax}).");
            foreach (var status in loaded.Statuses.Keys)
                if (pack.FindStatus(status) is null && pack.Statuses is not null && status > GamePack.ExpMaxStatusCode)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스테이터스 '{status}'가 스테이터스 마스터에 없습니다.");
            loaded = loaded with { SkillPool = loaded.SkillPool.Select(skill =>
            {
                if (skill is null || pack.FindSkill(skill.SkillCode) is null)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스킬 '{skill?.SkillCode}'가 스킬 마스터에 없습니다.");
                return skill with { Statuses = CheckOwnerStatuses(pack, $"캐릭터 '{code}'의 스킬 '{skill.SkillCode}'", skill.Statuses, pack.MasterStatusesOfSkill(skill.SkillCode)) };
            }).ToList() };
            foreach (var (slot, index) in loaded.SkillSlots)
            {
                if (pack.FindSkillSlot(slot) is null)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스킬 슬롯 '{slot}'가 스킬 슬롯 마스터에 없습니다.");
                if (index < 0 || index >= loaded.SkillPool.Count)
                    throw new SaveFormatException($"세이브의 캐릭터 '{code}'의 스킬 슬롯 '{slot}'이 가리키는 번호 {index}가 스킬 풀(스킬 {loaded.SkillPool.Count}개)에 없습니다.");
            }
            characters[instanceId] = loaded;
        }

        // 플레이어(캐릭터 1)는 늘 보유한다. 세이브에 없으면(예전 세이브) 새 캐릭터 상태로 채운다.
        if (!characters.ContainsKey(GamePack.PlayerCharacterCode) && pack.FindCharacter(GamePack.PlayerCharacterCode) is not null)
        {
            var playerId = NextInstanceId(usedIds);
            usedIds.Add(playerId);
            characters[playerId] = pack.NewCharacterInstance(GamePack.PlayerCharacterCode, playerId, NextOrderNumber(characters.Values));
        }
        // 한 코드는 캐릭터 인벤토리 마스터의 carry_limit개까지만 가질 수 있다(플레이어는 한 명).
        foreach (var group in characters.Values.GroupBy(c => c.CharacterCode))
            if (group.Count() > CharacterLimitOf(pack, group.Key))
                throw new SaveFormatException(
                    $"세이브의 캐릭터 '{group.Key}'가 소지 한도({CharacterLimitOf(pack, group.Key)})보다 많습니다: {group.Count()}");

        // 불러온 뒤 보유 캐릭터마다 로드 후 캐릭터 서킷을 돌려 게임팩의 정의대로 캐릭터를 조작한다.
        foreach (var id in characters.Keys.ToList())
            characters[id] = new AfterLoadCharacterCircuit(pack, characters[id]).Execute() with { InstanceId = id };

        // 세이브의 캐릭터 키(인스턴스 ID, 또는 캐릭터 코드 줄임 표기)를 보유 인스턴스 ID로 푼다. 코드는 그 코드의 첫 인스턴스다. 없으면 null.
        string? Resolve(string key) =>
            IsUlidKey(key) ? (characters.ContainsKey(Ulid.Parse(key).ToString()) ? Ulid.Parse(key).ToString() : null)
            : long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? characters.FirstOf(code)?.InstanceId : null;

        // 이미 어느 유닛이나 오퍼레이터가 쓴 인스턴스: 코드만 적힌 유닛은 그 코드의 아직 쓰지 않은 첫 인스턴스로 푼다(다 썼으면 첫 인스턴스, 아래에서 중복으로 걸린다).
        var placed = new HashSet<string>();
        string? ResolveUnused(long code)
        {
            var all = characters.InstancesOf(code);
            var id = (all.FirstOrDefault(c => !placed.Contains(c.InstanceId)) ?? all.FirstOrDefault())?.InstanceId;
            if (id is not null) placed.Add(id);
            return id;
        }

        // 파티: 새 형식은 Parties(파티-A ~ 파티-P, 각각 이름 + 유닛[])와 CurrentParty, 그 전 형식은 PartyInstance 하나(파티-A),
        // 가장 예전 세이브는 Party(캐릭터 코드 배열)와 EquippedItems(캐릭터별 장비)다. 예전 세이브에서 파티에 없는 캐릭터가
        // 끼고 있던 장비는 보유로 돌아간다. 파티끼리는 캐릭터를 공유할 수 없다.
        var partyIds = Enumerable.Range(0, PartyCount).Select(_ => new string?[PartySlotCount]).ToArray();
        var partyNames = Enumerable.Range(0, PartyCount).Select(DefaultPartyNameOf).ToArray();
        var partyEquipment = new Dictionary<string, Dictionary<long, EquipRef>>();
        var partyLeaders = Enumerable.Range(0, PartyCount).Select(_ => new bool[PartySlotCount]).ToArray();

        // 저장된 유닛이 가리키는 보유 인스턴스 ID(인스턴스 ID가 있으면 그것, 없으면 코드로 푼다).
        string ResolveUnit(PartyUnit unit)
        {
            string? id;
            if (!string.IsNullOrEmpty(unit.InstanceId))
            {
                id = Resolve(unit.InstanceId) ?? throw new SaveFormatException($"세이브의 파티 캐릭터 '{unit.InstanceId}'가 보유 캐릭터에 없습니다.");
                placed.Add(id);
                if (unit.CharacterCode != 0 && unit.CharacterCode != characters[id].CharacterCode)
                    throw new SaveFormatException($"세이브의 파티 유닛에 적힌 캐릭터 코드 '{unit.CharacterCode}'가 인스턴스 '{id}'의 코드와 다릅니다.");
                return id;
            }
            return ResolveUnused(unit.CharacterCode) ?? throw new SaveFormatException($"세이브의 파티 캐릭터 '{unit.CharacterCode}'가 보유 캐릭터에 없습니다.");
        }

        void ReadParty(int index, PartyInstance? saved)
        {
            if (saved is null) return;
            if (!string.IsNullOrWhiteSpace(saved.Name)) partyNames[index] = saved.Name;
            var savedUnits = saved.Units ?? [];
            if (savedUnits.Length > PartySlotCount)
                throw new SaveFormatException($"세이브의 파티 슬롯이 {PartySlotCount}칸보다 많습니다: {savedUnits.Length}");
            for (var i = 0; i < savedUnits.Length; i++)
            {
                if (savedUnits[i] is not { } unit) continue;
                var id = ResolveUnit(unit);
                partyIds[index][i] = id;
                partyLeaders[index][i] = unit.IsLeader;
                if (unit.Equipment is { Count: > 0 }) partyEquipment[id] = unit.Equipment;
            }
        }

        if (save.Parties is { } savedParties)
        {
            if (savedParties.Length > PartyCount)
                throw new SaveFormatException($"세이브의 파티가 {PartyCount}개보다 많습니다: {savedParties.Length}");
            for (var p = 0; p < savedParties.Length; p++) ReadParty(p, savedParties[p]);
        }
        else if (save.PartyInstance is not null) ReadParty(0, save.PartyInstance);
        else
        {
            if (save.Party is { } saved)
            {
                if (saved.Length > PartySlotCount)
                    throw new SaveFormatException($"세이브의 파티 슬롯이 {PartySlotCount}칸보다 많습니다: {saved.Length}");
                for (var i = 0; i < saved.Length; i++)
                    if (saved[i] is { } legacyCode)
                        partyIds[0][i] = ResolveUnused(legacyCode) ?? throw new SaveFormatException($"세이브의 파티 캐릭터 '{legacyCode}'가 보유 캐릭터에 없습니다.");
            }
            foreach (var (key, slots) in save.EquippedItems ?? [])       // 전부 검증하고, 파티에 없는 캐릭터의 장비는 아래에서 뺀다
                partyEquipment[Resolve(key) ?? key] = slots;
        }
        var currentParty = save.CurrentParty ?? 0;
        if (currentParty is < 0 or >= PartyCount)
            throw new SaveFormatException($"세이브의 현재 파티 '{currentParty}'가 0~{PartyCount - 1} 범위를 벗어납니다.");

        // 아지트의 층별 방어 파티(1층부터): 보통 파티와 같은 형식이고 파티끼리 캐릭터를 공유할 수 없다. 층이 없으면 1층뿐이다.
        // 현재 아지트(CurrentHideout, 없으면 시작 아지트)의 상태는 HideoutParties 등에, 나머지 아지트의 상태는 OtherHideouts에 있다.
        var currentHideout = save.CurrentHideout ?? pack.StartHideoutCode;
        if (!pack.HideoutCodes.Contains(currentHideout))
            throw new SaveFormatException($"세이브의 현재 아지트 '{currentHideout}'가 아지트 마스터에 없습니다.");
        var hideoutSaves = new Dictionary<long, HideoutSave> { [currentHideout] = new(save.HideoutParties, save.HideoutFacilities, save.HideoutOperators) };
        foreach (var (code, other) in save.OtherHideouts ?? [])
        {
            if (!pack.HideoutCodes.Contains(code))
                throw new SaveFormatException($"세이브의 아지트 '{code}'가 아지트 마스터에 없습니다.");
            if (code == currentHideout)
                throw new SaveFormatException($"세이브의 아지트 '{code}'가 현재 아지트와 OtherHideouts에 모두 있습니다.");
            hideoutSaves[code] = other ?? new HideoutSave();
        }
        var floorIdsOf = new Dictionary<long, string?[][]>();
        var floorLeadersOf = new Dictionary<long, bool[][]>();
        foreach (var (hideoutCode, hideoutSave) in hideoutSaves)
        {
            var floorSaved = hideoutSave.Parties is { Length: > 0 } savedFloors ? savedFloors : [null];
            var floorIds = floorSaved.Select(_ => new string?[PartySlotCount]).ToArray();
            var floorLeaders = floorSaved.Select(_ => new bool[PartySlotCount]).ToArray();
            var where = pack.Hideouts is null ? "" : $"아지트 '{hideoutCode}'의 ";
            for (var f = 0; f < floorSaved.Length; f++)
            {
                var savedUnits = floorSaved[f]?.Units ?? [];
                if (savedUnits.Length > PartySlotCount)
                    throw new SaveFormatException($"세이브의 {where}{f + 1}층 방어 파티의 슬롯이 {PartySlotCount}칸보다 많습니다: {savedUnits.Length}");
                for (var i = 0; i < savedUnits.Length; i++)
                {
                    if (savedUnits[i] is not { } unit) continue;
                    var id = ResolveUnit(unit);
                    floorIds[f][i] = id;
                    floorLeaders[f][i] = unit.IsLeader;
                    if (unit.Equipment is { Count: > 0 }) partyEquipment[id] = unit.Equipment;
                }
            }
            floorIdsOf[hideoutCode] = floorIds;
            floorLeadersOf[hideoutCode] = floorLeaders;
        }

        var captiveIds = new Dictionary<long, List<string>>();
        foreach (var (dungeon, units) in save.DungeonCaptives ?? [])
        {
            if (pack.Dungeons?.Any(d => d.Code == dungeon) != true)
                throw new SaveFormatException($"세이브의 구금된 아군이 갇힌 던전 '{dungeon}'가 던전 마스터에 없습니다.");
            captiveIds[dungeon] = [];
            foreach (var unit in units ?? [])
            {
                var id = ResolveUnit(unit);
                captiveIds[dungeon].Add(id);
                if (unit.Equipment is { Count: > 0 }) partyEquipment[id] = unit.Equipment;
            }
        }

        var allIds = partyIds.Concat(floorIdsOf.Values.SelectMany(f => f)).SelectMany(p => p).OfType<string>().Concat(captiveIds.Values.SelectMany(ids => ids)).ToList();
        if (allIds.GroupBy(c => c).Any(g => g.Count() > 1))
            throw new SaveFormatException("세이브의 파티에 같은 캐릭터가 두 번 이상 있습니다(파티끼리도 공유할 수 없습니다).");

        var statusEffects = ValidateStatusEffects(pack, characters, save.StatusEffects, Resolve);
        var (uniqueItems, characterTags, equippedItems) =
            ValidateEquipment(pack, characters, inventory, save.UniqueItems, save.CharacterTags, partyEquipment, Resolve);
        PartyUnit UnitOfId(string id, bool leader) =>
            new(characters[id].CharacterCode, equippedItems.TryGetValue(id, out var slots) ? slots : [], leader, id);
        var parties = partyIds.Select((ids, p) => new PartyInstance(partyNames[p],
            ids.Select((id, i) => id is not null ? UnitOfId(id, partyLeaders[p][i]) : null).ToArray())).ToArray();

        string? savedSupport(string what, string? savedId, long? savedCode)
        {
            if (savedId is not null)
                return Resolve(savedId) ?? throw new SaveFormatException($"세이브의 {what} 캐릭터 '{savedId}'가 보유 캐릭터에 없습니다.");
            if (savedCode is { } code)
                return characters.FirstOf(code)?.InstanceId ?? throw new SaveFormatException($"세이브의 {what} 캐릭터 '{code}'가 보유 캐릭터에 없습니다.");
            return null;
        }
        var partnerId = savedSupport("파트너", save.PartnerId, save.Partner);
        var assistId = savedSupport("어시스트", save.AssistId, save.Assist);
        if (save.Turn is { } turn && pack.FindTurn(turn) is null)
            throw new SaveFormatException($"세이브의 턴 '{turn}'이(가) 턴 마스터에 없습니다.");
        if (partnerId is not null && partnerId == assistId)
            throw new SaveFormatException("세이브의 파트너와 어시스트가 같은 캐릭터입니다.");

        var hideoutLoaded = new Dictionary<long, (List<PartyInstance> Parties, List<long?> Facilities, Dictionary<(long, bool, long), string> Operators)>();
        var operatorIds = new List<string>();
        foreach (var (hideoutCode, hideoutSave) in hideoutSaves)
        {
            var floorIds = floorIdsOf[hideoutCode];
            var floorLeaders = floorLeadersOf[hideoutCode];
            var floors = floorIds.Length;
            var floorParties = floorIds.Select((ids, f) => new PartyInstance("",
                ids.Select((id, i) => id is not null ? UnitOfId(id, floorLeaders[f][i]) : null).ToArray())).ToList();

            var facilities = new List<long?>(new long?[floors]);
            for (var i = 0; i < floors && hideoutSave.Facilities is { } savedFacilities && i < savedFacilities.Length; i++)
            {
                if (savedFacilities[i] is { } facility && pack.FindFacility(facility) is null)
                    throw new SaveFormatException($"세이브의 아지트 시설 '{facility}'가 시설 마스터에 없습니다.");
                facilities[i] = savedFacilities[i];
            }

            var operators = new Dictionary<(long, bool, long), string>();
            foreach (var op in hideoutSave.Operators ?? [])
            {
                if (op.FloorOrder < 1 || op.FloorOrder > floors)
                    throw new SaveFormatException($"세이브의 오퍼레이터 층 순서 '{op.FloorOrder}'가 1~{floors} 범위를 벗어납니다.");
                string? operatorId;
                if (!string.IsNullOrEmpty(op.InstanceId))
                {
                    operatorId = Resolve(op.InstanceId);
                    if (operatorId is not null) placed.Add(operatorId);
                }
                else operatorId = ResolveUnused(op.CharacterCode);
                if (operatorId is null)
                    throw new SaveFormatException($"세이브의 오퍼레이터 캐릭터 '{(string.IsNullOrEmpty(op.InstanceId) ? op.CharacterCode : op.InstanceId)}'가 보유 캐릭터에 없습니다.");
                if (!operators.TryAdd((op.FloorOrder, op.Built, op.SlotCode), operatorId))
                    throw new SaveFormatException($"세이브의 오퍼레이터 슬롯 (층 {op.FloorOrder}, 슬롯 {op.SlotCode})이 두 번 있습니다.");
            }
            operatorIds.AddRange(operators.Values);
            hideoutLoaded[hideoutCode] = (floorParties, facilities, operators);
        }
        if (operatorIds.GroupBy(c => c).Any(g => g.Count() > 1))
            throw new SaveFormatException("세이브에서 같은 캐릭터가 오퍼레이터 슬롯 두 곳 이상에 있습니다(한 곳에만 있을 수 있습니다).");
        if (operatorIds.Any(c => allIds.Contains(c)))
            throw new SaveFormatException("세이브에서 같은 캐릭터가 파티와 아지트에 모두 있습니다(한 곳에만 있을 수 있습니다).");

        foreach (var id in captiveIds.Values.SelectMany(ids => ids))
            if (pack.CapturedAllyTagCode is not { } allyTag || !characterTags.TryGetValue(id, out var allyTags) || !allyTags.Contains(allyTag))
                throw new SaveFormatException($"세이브에서 던전에 갇힌 캐릭터 '{id}'에 구금된 아군 태그가 없습니다.");
        if (pack.CapturedEnemyTagCode is { } capturedTag
            && allIds.Concat(operatorIds).FirstOrDefault(id => characterTags.TryGetValue(id, out var tags) && tags.Contains(capturedTag)) is { } captured)
            throw new SaveFormatException($"세이브에서 구금된 적 '{captured}'가 파티나 아지트에 있습니다(구금된 적은 교류만 할 수 있습니다).");

        // 불러오기는 저장된 화면과 상관없이 항상 홈으로 들어간다.
        var session = new GameSession(pack, Screen.Home, playerFlags, inventory, characters, parties, statusEffects, uniqueItems, characterTags)
        {
            CurrentPartyIndex = currentParty,
            PartnerId = partnerId,
            AssistId = assistId,
            TurnCount = save.Turn ?? pack.FirstTurn.TurnCount,
        };
        session.currentHideoutCode = currentHideout;
        foreach (var (hideoutCode, (floorParties, facilities, operators)) in hideoutLoaded)
        {
            var state = session.HideoutStateOf(hideoutCode);
            state.Parties.Clear();
            state.Parties.AddRange(floorParties);
            foreach (var (key, id) in operators) state.Operators[key] = id;
            state.Facilities.Clear();
            state.Facilities.AddRange(facilities);
        }
        foreach (var (dungeon, ids) in captiveIds) session.DungeonCaptives[dungeon] = ids.Select(id => UnitOfId(id, false)).ToList();
        return session;
    }

    /// <summary>세이브의 키가 ULID(인스턴스 ID)인가. 캐릭터 코드 줄임 표기(정수)는 아니다.</summary>
    static bool IsUlidKey(string key) => key.Length == 26 && Ulid.TryParse(key, out _);
}
