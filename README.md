# 🦝 Ponpoko (폰포코) — Unity Remake

> 1982년 세이부 전자 개발 / 시그마 발매의 전설의 아케이드 게임 **폰포코(ポンポコ, 한국명: 너구리)** 를 Unity로 재현한 2D 레트로 플랫포머 프로젝트입니다.


---

## 📖 프로젝트 소개

**폰포코**는 1980년대 한국 오락실을 대표했던 고전 아케이드 게임으로, 너구리 주인공이 발판과 사다리를 오르내리며 맵에 놓인 과일과 채소를 모두 먹으면 다음 스테이지로 넘어가는 방식입니다. 적들을 피하고, 점수를 높이며 스테이지를 클리어하는 것이 목표입니다.

이 프로젝트는 원작의 도트 그래픽과 감성을 그대로 살리면서, 현대적인 Unity 엔진으로 재구현한 팬 메이크 리메이크 프로젝트입니다.

### ✨ 주요 특징

- 🎮 **원작 재현** — 원작의 스테이지 구조, 아이템, 적 패턴을 충실히 재현
- 🎨 **레트로 도트 그래픽** — 오리지널 픽셀 아트 스프라이트 및 애니메이션
- 🎵 **원작 사운드** — 8비트 감성의 BGM 및 효과음
- ⌨️ **아케이드 감성 조작** — 원작 그대로의 키보드 조작감

---

## 🛠 기술 스택

| 구분 | 내용 |
|------|------|
| **엔진** | Unity 2022.3.60f1 (LTS) |
| **렌더 파이프라인** | 2D Built-in Render Pipeline |
| **주요 패키지** | Unity 2D Feature 2.0.1, TextMeshPro 3.0.7, uGUI, Timeline 1.7.6 |
| **폰트** | NeoDunggeunmo (네오둥근모), BoldPixels |
| **언어** | C# (.NET Standard 2.1) |

---

## 📁 프로젝트 구조

```
Assets/
├── animation/          # 애니메이션 클립 및 Animator Controller
│   ├── player.controller              # 플레이어 메인 애니메이터
│   ├── character-jump-land.controller # 타이틀/인트로용 애니메이터
│   ├── run.anim / jump.anim / ladder.anim
│   ├── death.anim / dance.anim / intro_run.anim
│   └── ...
├── Audios/             # BGM 및 효과음 에셋
├── BoldPixels/         # 레트로 픽셀 폰트
├── pal/                # 팔레트 스프라이트 시트
├── prefab/             # 게임 오브젝트 프리팹 (플레이어, 적, 아이템 등)
├── Scenes/             # 게임 씬 (타이틀, 스테이지 등)
├── script/             # C# 게임 로직 스크립트
├── Sprites/            # 도트 스프라이트 에셋 (캐릭터, 배경, UI)
└── UI prefab/          # UI 프리팹 (HUD, 메뉴 등)
```

### 🎭 플레이어 애니메이션 상태

`player.controller` 에서 관리되는 플레이어 상태:

| 상태 | 설명 | 전환 조건 |
|------|------|-----------|
| **Idle** | 대기 (기본 상태) | — |
| **run** | 좌우 이동 | `IsRunning = true` |
| **jump** | 점프 | `Jump` 트리거 |
| **ladder** | 사다리 오르내리기 | `IsClimbing = true` (속도: `ClimbSpeed`) |
| **death** | 사망 | `Die` 트리거 |

**Animator 파라미터**: `IsRunning`, `IsClimbing`, `Jump`, `IsGrounded`, `ClimbSpeed`, `Die`

---

## 🚀 설치 및 실행

### 요구 사항

- Unity Hub
- Unity Editor **2022.3.60f1** (LTS) — [다운로드](https://unity.com/releases/editor/archive)
- Windows / macOS / Linux

### 실행 방법

1. 저장소를 클론합니다:
   ```bash
   git clone https://github.com/<your-username>/ponpoko-unity.git
   cd ponpoko-unity
   ```

2. Unity Hub에서 **Open → Add project from disk** 로 프로젝트 폴더를 추가합니다.

3. Unity Editor로 프로젝트를 엽니다 (첫 실행 시 패키지 임포트로 시간이 소요됩니다).

4. `Assets/Scenes/` 폴더에서 **Title 씬**을 열고 **Play** 버튼을 누릅니다.

### 빌드 방법

1. 메뉴에서 **File → Build Settings** 를 엽니다.
2. 타겟 플랫폼(Windows/macOS/Linux)을 선택하고 **Switch Platform** 을 클릭합니다.
3. **Build** 를 눌러 실행 파일을 생성합니다.

---

## 🎮 조작법

| 키 | 동작 |
|----|------|
| `←` `→` / `A` `D` | 좌우 이동 |
| `↑` `↓` / `W` `S` | 사다리 오르내리기 |
| `Space` | 점프 |
| `Space` (타이틀 화면) | 게임 시작 |

---

## 🎯 게임 플레이

### 목표
- 맵에 배치된 **모든 과일과 채소(사과, 딸기, 수박, 포도, 당근, 버섯 등)** 를 먹으면 스테이지 클리어!
- 적 캐릭터와 접촉하면 생명이 차감됩니다.
- 제한 시간 내에 클리어하면 보너스 점수를 얻습니다.

## 🎨 에셋 정보

- **스프라이트**: 원작 게임 그래픽을 기반으로 한 픽셀 아트
- **폰트**: [NeoDunggeunmo](https://neodgm.dalgona.dev/) (네오둥근모) — 배포용 무료 픽셀 폰트
- **사운드**: 8비트 칩튠 스타일 BGM 및 효과음


---

## 📜 라이선스

이 프로젝트는 학습 및 팬 활동 목적으로 제작된 비상업적 프로젝트입니다.
원작 게임(폰포코/너구리)의 모든 저작권은 원 저작권자에게 있습니다.

---

<div align="center">


</div>
