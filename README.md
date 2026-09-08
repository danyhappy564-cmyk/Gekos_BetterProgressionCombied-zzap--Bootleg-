# Gekos_BetterProgression Combined — SPT 4.1 포팅

원작 **Geko's Better Progression** (DrunkGeko, 기여: marbL-) 을 **SPT 4.1.5** 로 포팅한 저장소입니다.
원본: https://forge.sp-tarkov.com/mod/2088/gekos-better-progression · 라이선스 MIT

레벨 진행·스킬·상인·은신처·플리 등을 통째로 재조정하는 대형 서버 모드 + 스킬 포인트 UI를 붙이는 클라 플러그인, 두 개가 한 세트입니다.

| 프로젝트 | 설치 위치 | 하는 일 |
| --- | --- | --- |
| `GekosBetterProgression` (서버) | `SPT\user\mods\Gekos_BetterProgression\` | 아이템/상인/은신처/퀘스트/플리 밸런스 변경 전부 |
| `gekos_api` (클라) | `BepInEx\plugins\gekos_api\` | 스킬 탭의 +/- 버튼, 남은 스킬포인트 UI, 스킬 XP·버프 배율, GP 아이콘/최저가 수정 |

## 빌드

```
dotnet build Gekos_BetterProgressionCombined.sln -c Release
```

SPT 경로 기본값은 `E:\SPT 4.1` 입니다. 다르면 `-p:SptRoot="D:\내경로"` 를 붙이세요.
빌드하면 위 표의 경로로 자동 복사됩니다.

---

## 4.1 포팅에서 바뀐 것

### 1. 클라이언트 — 난독화가 풀리면서 이름이 전부 바뀜

SPT 4.1은 EFT 클라이언트의 난독화를 해제합니다. 실제 4.1.5 `Assembly-CSharp.dll` 로 하나하나 확인해서 아래처럼 맞췄습니다.

| 4.0 (난독화) | 4.1 (실제 이름) | 어디서 쓰나 |
| --- | --- | --- |
| `SkillClass` | `EFT.Skill` | 전부 |
| `AbstractSkillClass` | `EFT.BaseSkill` | 추가 스킬 레벨 |
| `SkillClass.method_3()` | `Skill.UpdateRules()` | 버프 재계산 |
| `SkillClass.method_4()` | `Skill.CalculateRealEarnedExpForLobby()` | 네이티브 레벨 노출 |
| `SkillManager.SkillBuffClass` | `SkillManager.FloatBuff` | 스킬 버프 배율 |
| `…SkillBuffClass.Class1425/6/7/8` | `FloatBuff.CG_PerLevel / CG_Max / CG_Custom / CG_Elite` | 스킬 버프 배율 |
| 위 클래스들의 `SkillBuffClass` 필드 | `FloatBuff` | 스킬 버프 배율 |
| `SkillPanel.method_1()` | `SkillPanel.OnSkillLevelChanged()` | +/- 누른 뒤 패널 갱신 |
| `SkillIcon.skillClass` | `SkillIcon._skill` | 레벨 표시 갱신 |
| `TraderClass` | `EFT.Trading.Trader` | 최저가 수정 |
| `TraderClass.GStruct300` | `Trader.ItemPrice` | 최저가 수정 |
| `TraderClass.SupplyData_0` | `Trader._supplyData` | 최저가 수정 |
| `GClass3130` | `EFT.InventoryLogic.CurrencyUtil` | 최저가 수정 |
| `ISession` | `EFT.IEftSession` | 프로필 조회 (spt-common 경유라 소스 변경 없음) |

`Class1425`~`Class1428` 은 `FloatBuff.PerLevel / Max / Custom / Elite` 안의 람다가 만들어내는
컴파일러 생성 클로저 클래스입니다. 4.1이 이걸 `CG_*` 라는 읽을 수 있는 이름으로 바꿔줘서,
버전이 올라가도 번호가 밀려 깨질 일이 사라졌습니다.

### 2. 클라이언트 — 조용히 깨질 뻔한 것 두 개 (실제 버그 수정)

4.1에서 **private 이던 필드가 public 으로 바뀐** 곳이 있습니다. 원본 코드는 `BindingFlags.NonPublic`
만 넘기고 있어서, 4.1에서는 **예외도 안 나고 그냥 `null` 을 돌려받아** 기능이 조용히 죽습니다.

- `SkillIcon._levelPanel` → public. 그대로 뒀다면 +/- 눌러도 레벨 숫자가 안 바뀜
- `TradingItemView._currency` → public. 그대로 뒀다면 GP 아이콘 수정이 통째로 무시됨

둘 다 `Public | NonPublic | Instance` 로 고쳤습니다.

### 3. 클라이언트 — 4.1이 추가한 이름 충돌

4.1 클라에 **전역 네임스페이스 `Utils` 클래스**가 새로 생겼습니다. C# 이름 조회 규칙상 이게
`using gekos_api.Helpers;` 보다 먼저 잡혀서, 원본의 `Utils.GetPlayerProfile()` 호출이 전부 컴파일
에러가 납니다. 모드 쪽 헬퍼를 `GekoUtils` 로 개명해서 해결했습니다.

### 4. 서버 — `DatabaseTables` 가 사라짐

4.1은 `DatabaseService` / `DatabaseServer` / `DatabaseTables` / `ConfigServer` 를 없애고,
테이블 하나하나를 개별 주입 대상으로 쪼갰습니다.

```csharp
// 4.0
DatabaseTables tables = databaseService.GetTables();
var questConfig = configServer.GetConfig<QuestConfig>();

// 4.1 — 필요한 테이블과 config를 생성자에서 직접 받는다
PostDBLoader(TemplateTable templateTable, TradersTable tradersTable,
             HideoutTable hideoutTable, GlobalTable globalTable, LocaleTable localeTable, ...)
PreSPTLoader(..., QuestConfig questConfig, ...)
```

밸런스 스크립트가 20개가 넘어서 전부 고치는 대신, 옛날 `tables.Templates.Items` 모양을 그대로
유지하는 얇은 `DatabaseTablesView` 를 만들어 `Context` 에 물렸습니다. 덕분에 **변경 스크립트 본문은
한 줄도 안 건드렸습니다.**

### 5. 서버 — 그 밖의 4.1 변경

| 항목 | 4.0 | 4.1 |
| --- | --- | --- |
| 패키지 | `SPTarkov.*` 4.0.0 | `SPTushonka.*` 4.1.5 |
| 타깃 | `net9.0` | `net10.0` |
| 메타데이터 | `AbstractModMetadata` 상속 | `IModMetadata` 구현 + `HasPrepatcher` 추가, `IsBundleMod` 삭제 |
| 진입점 | `IOnLoad.OnLoad()` | `IOnLoad.OnLoadAsync(CancellationToken)` |
| 로드 순서 | `OnLoadOrder.PreSptModLoader` | `OnLoadOrder.Preload` (값은 100000으로 동일) |
| 라우터 핸들러 | 인자 4개 | 인자 5개 (`CancellationToken` 추가) |
| `ISptLogger` | `Models.Utils` | `SPTarkov.Common.Models.Logging` |
| 헬퍼 네임스페이스 | `Core.Helpers` | `Helpers.Items` / `Helpers.Profile` / `Helpers.Server` |
| `LocaleService` | `Core.Services` | `Services.Locales` |
| `LocationLifecycleService` | `Core.Services` | `Services.InRaid` |
| `Reward.TraderId` | `string` | `StringOrInt` |
| 글로벌 config 타입 | `Eft.Common.Config` | `Spt.Tables.GlobalConfig` |

### 6. 서버 — Ref 평판 패치가 async가 됨 (중요)

`LocationLifecycleService.EndLocalRaid` 가 4.1에서 **`EndLocalRaidAsync`** 로 바뀌었습니다.
async 메서드에 그냥 Postfix를 달면 **Task를 돌려주는 순간** 실행돼서, 레이드 결과가 프로필에
기록되기 *전에* 평판이 더해지고 그대로 덮어써집니다. 그래서 반환 Task를 감싸는 방식으로 바꿨습니다.

```csharp
[PatchPostfix]
static void Postfix(ref Task __result, MongoId sessionId, EndLocalRaidRequestData request)
{
    __result = AwardAfter(__result, sessionId, request);   // 원본을 await 한 뒤에 평판 지급
}
```

호출부가 우리 Task를 await 하므로, 평판이 반영된 뒤에 응답이 나갑니다.

### 7. 정리한 것

- 3.11 타입스크립트 시절 잔재 삭제: `package.json`, `package-lock.json`, `mod.code-workspace`
- 클라 프로젝트를 구식 `.csproj`(`v4.7.1` + `postBuildEvent` 배치 스크립트)에서 **SDK 스타일
  `netstandard2.1`** 로 교체. 하드코딩된 `..\..\EscapeFromTarkov_Data\` 상대경로 대신 `SptRoot` 사용
- 루트에 두 프로젝트를 함께 여는 `.sln` 추가
- 스킬 버프 배율 패치에서 `dynamic` 제거 → `SkillManager.FloatBuff` 로 정적 타이핑
  (`Microsoft.CSharp` 런타임 의존이 사라지고, 오타가 컴파일 타임에 잡힘)

---

## 검증

컴파일이 되는 것과 런타임에 Harmony가 대상을 찾는 것은 다른 문제라, 실제 4.1.5
`Assembly-CSharp.dll` 을 상대로 **패치 대상 41개를 전부 조회해보는 하네스**를 돌렸습니다.

- 패치 대상 메서드/프로퍼티가 존재하는가, **오버로드가 모호하지 않은가**
- 리플렉션으로 찾는 필드가 **모드가 넘기는 `BindingFlags` 로 실제로 잡히는가**
- 필드 타입이 기대한 타입인가

결과: **41/41 통과**.

## 아직 확인 못한 것

- **인게임 테스트는 안 했습니다.** 아래는 오프라인에서 검증 불가능한 항목입니다.
- 스킬 탭 UI는 `"Skill Icon"` / `"Level Panel"` / `"TopPanel"` / `"Progress Panel"` / `"Current Text"`
  같은 **게임오브젝트 이름**을 찾아 들어갑니다. BSG가 UI 계층을 바꿨다면 그 부분만 조용히 안 뜹니다.
- `Assets/skillsbutton.bundle` 은 원작이 예전 유니티로 구운 번들입니다. EFT 유니티 버전이 올라갔다면
  번들을 다시 구워야 할 수 있습니다.
- `SPT.Common.Http.RequestHandler.GetJson` 은 4.1용 `spt-common.dll` 이 손에 없어서 4.0 빌드로만
  확인했습니다 (SPT 자체 코드라 바뀌었을 가능성은 낮습니다).
