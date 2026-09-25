# 🎮 웹 빌드: [gammja17.github.io/BeatChef](https://gammja17.github.io/BeatChef/)

### 📱 모바일 빌드 (APK / iOS) 다운로드: [Google Drive LINK](https://drive.google.com/drive/folders/1Wd_l3OtEhGpv75bVXIjgq0dE7vxzCYv2?usp=sharing)
### 🎬 시연 영상: [(YouTube)](https://www.youtube.com/watch?v=ivl30xvtJHI)

---

# BEATCHEF (비트셰프)
> 음악에 심취한 요리사가 비트에 맞춰 재료를 썰어 요리를 완성하는 리듬 액션 게임.
> 요리사와 검사(劍士)는 한 끗 차이.
<img width="636" height="394" alt="image" src="https://github.com/user-attachments/assets/302f9530-3f39-4695-bb54-1e139d03d24d" />


웹은 브라우저에서 설치 없이 바로 실행됩니다 (Chrome 권장). NHN 해커톤 출품작입니다.

## 특징

- **아무 곡이나 스테이지가 된다** — 곡을 분석해 템포/비트를 추정하고 노트를 자동 배치.
  기본 제공 곡(팀 Suno AI 자작곡) 외에 내 mp3를 업로드해 바로 플레이 가능 (웹에서도!)
- **요리 리듬 액션** — 사방에서 날아오는 재료를 방향키로 썰기. 과즙 파티클, 참격,
  히트스톱, 비트 동기 컷신 카메라
- **3D 픽셀 룩** — 3D 씬을 저해상도 렌더링해 만드는 도트 감성
- 연타·홀드 특수 노트, 20콤보 피버 타임(무지개 황홀경), 요리 4종 선택, 난이도 3종,
  모드 2종(클래식/이지), 내장곡 3곡 + 곡 미리듣기, 곡×난이도별 하이스코어·기록 버블

## 조작

| 입력 | 동작 |
|---|---|
| 방향키 / WASD | 해당 방향 썰기 |
| 마우스 스와이프 | 그은 방향 썰기 |
| ESC | 일시정지 · 뒤로 |
| F1 / F2 | 판정 타이밍 보정 |

## 개발 환경 / 빌드

- Unity 6000.3.11f1, URP
- 클론 후 Unity로 열면 첫 오픈 시 URP가 자동 구성됩니다
- 원클릭 툴 (메뉴 `BeatSlash`): 씬 생성, 재료 프리팹 일괄 생성, 내장곡 비트맵 베이크,
  WebGL 빌드(→ `docs/`, GitHub Pages 서빙)
- 웹 배포: `BeatSlash > Build WebGL (docs)` 실행 후 커밋·푸시하면 Pages가 자동 갱신

## 사용 에셋 고지

**유료·Asset Store 에셋은 라이선스상 이 리포에 포함되어 있지 않습니다.** 각 에셋의 권리는 원 제작자에게 있습니다.
에디터에서 열려면 아래 **필수** 에셋을 직접 import해야 합니다 (없으면 `PixelCamera` 컴파일 에러, 이펙트 참조 누락).

| 에셋 | 제작자 | 라이선스 | 리포 포함 |
|---|---|---|---|
| Critter 3D Pixel Camera (**필수**) | Nimble Fox | Unity Asset Store (유료) | ✗ |
| Hyper Casual FX Pack Vol.2 (**필수**) | Kyeoms | Unity Asset Store (유료) | ✗ |
| Toon FX | Archanor VFX | Unity Asset Store (유료) | ✗ |
| Synty POLYGON Prototype | Synty Studios | Unity Asset Store (유료) | ✗ |
| Koreographer | Sonic Bloom | Unity Asset Store (유료) | ✗ |
| Music Beat - Audio Visualizer | Sindri Studios | Unity Asset Store (유료) | ✗ |
| Kenney Food Kit / Impact Sounds | Kenney | CC0 | ✓ |
| DOTween | Demigiant | 무료 | ✓ |
| 전주완판본체 (폰트) | 전주시 | 무료 (임베드 허용) | ✓ |

- 음악(I COOKED 등 내장곡)은 팀이 **Suno AI로 직접 제작**한 자작곡입니다
- 로고·키아트·UI 아트는 생성형 AI로 제작 후 가공했습니다
