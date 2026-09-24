### ⚠️ IMPORTANT NOTICE / DISCLAIMER

**Original Author:** DrunkGeko (contributor: marbL-)
**Original Repository:** Geko's Better Progression
**Original Link:** https://forge.sp-tarkov.com/mod/2088/gekos-better-progression
**License:** MIT
**This Port By:** R_F (danyhappy564-cmyk) — unofficial, AI-assisted port. Not affiliated with or endorsed by the original author.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

## 변경 이력

- 2026-09-24 21:50 (KST) — 시큐어 컨테이너 퀘스트 보상 보강. 피드백: Delivery from the Past → Alpha,
  Setup → Beta, Network Provider - Part 1 수락 → Gamma 가 안 들어옴.
  - 실제 SPT 4.1.6 서버에 이 모드만 올려 퀘스트를 수락/완료해 본 결과 **세 컨테이너 모두 우편으로 정상 지급됨** —
    모드 단독으로는 재현되지 않았다.
  - 대신 **다른 모드가 나중에 퀘스트 데이터를 통째로 덮어쓰면** 이 모드가 넣은 보상이 조용히 사라진다(로그 없음).
    이제 서버 로딩이 거의 끝난 시점(`PostLoad + 60000`)에 한 번 더 확인해서, 사라졌으면 다시 넣는다.
    테스트용 "보상 지우는 모드"를 같이 올려 복구 → 우편 지급까지 확인.
  - 서버 로그에 결과가 남는다: `Added 3 secure container quest rewards`, 로딩 끝에
    `All secure container quest rewards present after load`(정상) 또는
    `... were removed by another mod after loading; restored N`(다른 모드가 지웠고 복구함).
  - 설정에 적힌 퀘스트가 DB에 없으면 서버가 예외로 보상 추가 전체를 건너뛰던 것을, 그 퀘스트만 경고 후 건너뛰게 변경.
    컨테이너 크기 변경이 실패해도 보상은 먼저 들어가도록 순서도 바꿈.
  - 워크벤치 제작: 모드 적용 전/후 레시피 228개를 전부 비교했고 빠진 레시피는 없음. 탄약 제작 16개가 설정
    (`algorithmicalRebalancing` → `ammoRules.craftSettings`)에 따라 **워크벤치 요구 레벨이 바뀌는 건 원작 설계**
    (예: 9x18mm PBM 은 1→3 레벨로 올라감). 퀘스트로 해금되는 제작 7개가 안 풀리는 현상은 모드 없는
    순정 서버에서도 똑같이 나와서 이 모드 문제가 아님.
- 2026-09-11 — 저작권/면책 헤더 추가, 릴리즈 zip 빌드 타깃 추가.
- 2026-09-08 — SPT 4.1.5 포팅 (아래 "4.1 포팅에서 바뀐 것" 참고).

---

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

### 6. 서버 — `[Injectable]` 기본 수명이 바뀜 (이게 서버를 죽였던 원인)

이게 제일 안 보이는 함정이었습니다.

| | 4.0 | 4.1 |
| --- | --- | --- |
| `[Injectable]` 기본값 | `InjectionType.Scoped` | **`InjectionType.Transient`** |

`Scoped` 는 서버 기동 중엔 사실상 하나만 생기지만, `Transient` 는 **주입될 때마다 새 객체**를
만듭니다. 이 모드는 `PreSPTLoader` 에서 설정을 읽어 `Context` 에 담고 `PostDBLoader` 에서 꺼내
쓰는 구조라, 4.1에서는 서로 다른 `Context` 를 받게 되어 이렇게 죽습니다.

```
[Critical][SPTarkov.Server.Core] The server has unexpectedly stopped...
System.Exception: Context was not initialized!
   at GekosBetterProgression.PostDBLoader.OnLoadAsync(...)
```

`Context` 를 명시적으로 싱글톤으로 등록해서 해결했습니다.

```csharp
[Injectable(InjectionType.Singleton)]   // 4.0에선 [Injectable] 만으로 충분했음
public class Context { ... }
```

> 4.0에서 넘어온 서버 모드 중 **로드 단계 사이에 상태를 넘기는 클래스**가 있다면 전부 같은 문제를
> 겪습니다. 컴파일도 되고 경고도 안 뜨니 서버가 죽어야 알게 됩니다.

### 7. 서버 — Ref 평판 패치가 async가 됨 (중요)

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

### 8. 정리한 것

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

다만 이 하네스는 *클라이언트* 바인딩만 봅니다. 서버 쪽 DI 수명 문제(위 6번)는 어셈블리를 봐도
알 수 없고 실제로 띄워봐야 나오는 종류라, 실기동 로그로 잡았습니다.

## 아직 확인 못한 것

- 위 6번(`Context` 싱글톤)은 실기동 로그에서 잡은 원인을 고친 것이고, **고친 뒤 다시 띄워본 것은
  아닙니다.** 재기동 확인 필요.
- **인게임 테스트는 안 했습니다.** 아래는 오프라인에서 검증 불가능한 항목입니다.
- 스킬 탭 UI는 `"Skill Icon"` / `"Level Panel"` / `"TopPanel"` / `"Progress Panel"` / `"Current Text"`
  같은 **게임오브젝트 이름**을 찾아 들어갑니다. BSG가 UI 계층을 바꿨다면 그 부분만 조용히 안 뜹니다.
- `Assets/skillsbutton.bundle` 은 원작이 예전 유니티로 구운 번들입니다. EFT 유니티 버전이 올라갔다면
  번들을 다시 구워야 할 수 있습니다.
- `SPT.Common.Http.RequestHandler.GetJson` 은 4.1용 `spt-common.dll` 이 손에 없어서 4.0 빌드로만
  확인했습니다 (SPT 자체 코드라 바뀌었을 가능성은 낮습니다).
