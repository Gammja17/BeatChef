# 프로토타입 씬 세팅 (10분 코스)

## 1. 씬 구성

```
SampleScene
├─ Main Camera            ← 3DPixelCamera 픽셀화 컴포넌트 + CameraPunch
├─ Conductor              ← Conductor + AudioSource (빈 오브젝트)
├─ Systems                ← BeatJudge + HitStop + GameFlow + SlashController
├─ Spawner                ← IngredientSpawner
│   ├─ SpawnL / SpawnR    ← 화면 좌우 하단 밖 (spawnPoints)
│   └─ SliceZone          ← 화면 중앙, 도마 위치
└─ 환경 (도마, 주방 배경 등 — PolygonPrototype로 그레이박스)
```

## 2. 재료 프리팹

1. 임시로는 Sphere/Capsule에 색 머티리얼 (토마토=빨강 구, 오이=초록 캡슐)
2. `Sliceable` 붙이고: halfPrefab(반구 등), juiceFxPrefab(VFX_Klaus 폭발 계열), juiceColor
3. 나중에 Kenney Food Kit(무료 CC0, 잘린 반쪽 포함) 모델로 교체

## 3. 비트맵

1. 곡(mp3/wav)을 `Assets/Music/`에 넣기
2. 메뉴 `BeatSlash > Onset Beatmap Generator` → 클립 지정 → Generate
3. 생성된 `Assets/Beatmaps/<곡명>.json`을 GameFlow.beatmapJson에, 곡을 songClip에 지정

## 4. 픽셀 카메라

- `Assets/3DPixelCamera` 데모 씬에서 카메라 프리팹/컴포넌트 구성을 복사
- 내부 해상도는 360~480p부터 시작 (너무 낮으면 날아오는 재료 가독성 죽음)
- 판정 UI/주문서 UI는 픽셀화 대상에서 제외 (오버레이 캔버스)

## 5. 플레이

Play → 재료가 비트에 맞춰 날아옴 → 좌클릭/스페이스로 썰기
- Perfect: 히트스톱 + 카메라 펀치 + 과즙 폭발
- 판정이 어긋나면 Conductor.inputLatency로 보정 (+면 판정 늦춤, 보통 0.02~0.08)
