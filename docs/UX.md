# UX 명세

> 화면, 연출, 입력, 시각 규칙을 정합니다. 기능 범위와 완료 기준은 [PRD.md](PRD.md), 구현 구조는 [ARCHITECTURE.md](ARCHITECTURE.md)에 있습니다.

## 1. 컨셉
- 사이트는 "LOONACIAVERSE"라는 항성계다. 중심 항성은 사이트 주인(About)이고, 카테고리 행성들이 그 주위를 공전한다.
- 레퍼런스는 StarCraft II: Legacy of the Void의 Celestial Array(목적지 선택) 화면이다. 어두운 우주, 청록색 HUD 선, 영어 대문자 라벨, 선택한 행성을 감싸는 홀로그램 링, 하단 중앙의 TRAVEL 버튼을 따른다.
- 연출이 정보 접근을 방해하면 안 된다. 채용 담당자가 첫 화면에서 두 번 클릭 안에 프로젝트 상세에 닿을 수 있어야 한다(대표 프로젝트 카드 → 상세).

## 2. 화면과 라우트
| 라우트 | 화면 |
|---|---|
| `/` | 메인 화면(3D 항성계), 대체 시 목록형 홈 |
| `/?focus={id}` | 메인 화면에서 해당 천체에 포커스하고 HUD를 연 상태 |
| `/projects` | 프로젝트 목록 |
| `/projects/{slug}` | 프로젝트 상세(케이스 스터디) |
| `/blog` | 블로그 목록 |
| `/blog/tags/{tag}` | 태그별 글 목록 |
| `/blog/{slug}` | 글 상세 |
| `/about` | About(중심 항성) |
| 그 밖 | 404(LOST IN SPACE) |

## 3. 부팅 화면 (`index.html`)
- .NET 런타임을 받는 동안 보여 준다. 화면 중앙에 다음을 둔다.
  - `LOONACIAVERSE`(display 폰트)
  - `NAVIGATION SYSTEM BOOT`(HUD 폰트)
  - 진행 게이지와 퍼센트 텍스트. Blazor의 `--blazor-load-percentage`, `--blazor-load-percentage-text` CSS 변수로 채운다.
- 게이지 아래에 작은 일반 링크 `PROJECTS · BLOG · ABOUT`(`<a href>`)를 둔다. JS를 실행하지 않는 크롤러와 기다리기 싫은 사용자를 위한 것이다.
- 씬 번들(`js/scene.js`)은 `<link rel="modulepreload">`로 부팅과 동시에 받는다.
- `<noscript>`에서 JavaScript가 필요하다고 안내하고 같은 링크를 둔다.

## 4. 메인 화면

### 4.1 천체
이름은 임시이며 `content/universe.yml`에서 바꾼다.

| id | 이름(임시) | 부제 | 라우트 | 비고 |
|---|---|---|---|---|
| `about` | LOONACIA | ABOUT | `/about` | 중심 항성. 공전하지 않고 자체 발광(코로나 글로우)한다 |
| `projects` | ARTIFEX | PROJECTS | `/projects` | |
| `blog` | ARCHIVA | BLOG | `/blog` | |
| `playground` | ARCADIA | PLAYGROUND | - | `locked: true` |

- 각 천체의 표면, 대기, 색은 절차적 셰이더 파라미터(`visual`)로 정한다(ADR-007).
- 행성은 `visual.surface`로 표면 타입을 고른다. 같은 타입이어도 색과 노이즈 스케일로 달라진다.
  - `terran`: 바다와 대륙, 극지 얼음, 흐르는 구름, 밤쪽 도시 불빛
  - `gas`: 위도마다 다른 속도로 흐르는 띠, 타원형 폭풍, 기울어진 자전축과 적도면의 고리
  - `desert`: 화성처럼 녹슨 먼지 평원과 어두운 암석 지대, 극관, 적도를 가로지르는 긴 협곡, 빛에 따라 음영이 지는 크레이터
- 항성은 플라스마 소용돌이, 잔 쌀알 무늬, 흑점, 가장자리로 갈수록 어두워지는 주연 감광을 갖고, 코로나는 천천히 일렁인다.
- 배경은 크기와 색(흰색·푸른색·따뜻한 색·분홍·청록)이 다른 별 필드(포인트)와 청록·보라·분홍 성운으로 구성한다. 성운은 HUD 글자가 잘 읽히도록 블룸 임계값보다 어둡게 둔다.

### 4.2 카메라와 움직임
- **카메라**: 궤도면을 비스듬히 내려다보는 고정 시점이다. 사용자가 회전하거나 줌할 수 없다.
- **공전**: 행성은 항성 주위를 천천히 공전한다(한 바퀴 60~120초). 포인터가 천체 위에 있거나 포커스 상태이면 공전을 멈춘다. 클릭 대상이 움직이지 않게 하기 위해서다.
- **자전**: 모든 천체는 항상 천천히 자전한다.
- **배경의 움직임**: 마우스 입력으로는 카메라도 배경도 움직이지 않는다. 대신 배경이 스스로 움직인다. 별은 저마다 다른 위상과 속도로 반짝이고, 성운은 아주 천천히 흐르며, 5~14초 간격으로 유성이 화면 위쪽에서 비스듬히 떨어진다.
- **이름표**: 포커스되지 않은 천체 옆에 세계관 이름과 부제를 HTML 라벨로 띄운다. 레퍼런스의 "Shakuras" 라벨과 같다. 3D 위치를 따라다녀야 하므로 씬 모듈이 CSS2DRenderer로 관리한다(ADR-003의 예외). 이름표를 클릭하면 해당 천체를 클릭한 것과 같이 FOCUSED로 전환한다.

### 4.3 상태
```
OVERVIEW ──선택──▶ FOCUSED(id) ──TRAVEL──▶ WARPING(id) ──▶ 상세 페이지로 이동
    ▲                 │  ▲
    └──닫기/뒤로가기──┘  └── 다른 천체 선택(FOCUSED(id'))
```
- **OVERVIEW**: 상단 중앙에 `LOONACIAVERSE` / `SELECT DESTINATION`. 모든 천체에 이름표가 있고 공전이 진행된다.
- **FOCUSED(id)**: 카메라가 약 0.8초 ease-in-out으로 그 천체에 다가간다. 데스크톱에서는 천체를 화면 중앙에서 약간 오른쪽에 두어 왼쪽을 HUD 자리로 남긴다. 천체에 홀로그램 링이 나타나고 HUD가 열린다.
- **WARPING(id)**: TRAVEL을 누르면 1초 이내에 다음이 진행되고, 끝나면 라우팅한다.
  1. 카메라가 천체로 돌진한다.
  2. 별이 진행 방향으로 길게 늘어난다(star streak).
  3. 화면이 배경색으로 페이드된다.
- 잠긴 천체(`playground`)는 FOCUSED까지만 가고 WARPING으로 넘어가지 않는다.

### 4.4 URL 규칙
- OVERVIEW는 `/`, FOCUSED는 `/?focus={id}`다.
- OVERVIEW → FOCUSED는 history에 push한다. 그래서 뒤로가기를 누르면 HUD가 닫힌다.
- FOCUSED → 다른 천체 FOCUSED는 replace한다. 그래서 뒤로가기 한 번이면 OVERVIEW로 돌아간다.
- UI(✕, Esc, 빈 공간 클릭)로 닫을 때:
  - 이 세션에서 push한 포커스면 `history.back()`
  - `/?focus=`로 직접 들어온 경우면 `/`로 replace
- 알 수 없는 `focus` 값은 `/`로 replace한다.
- `/?focus={id}`로 직접 들어오면 줌인 애니메이션 없이 포커스 상태로 시작한다.

### 4.5 입력
| 입력 | 동작 |
|---|---|
| 클릭·탭(천체) | 그 천체로 FOCUSED |
| 클릭·탭(빈 공간), Esc, ✕ | 닫기(4.4) |
| 호버(천체) | 이름표 강조, 커서 pointer, 공전 정지 |
| Tab / Shift+Tab | 천체 버튼 사이 이동. 포커스된 버튼의 천체가 호버처럼 강조된다 |
| ←/→ (FOCUSED 상태) | 이전/다음 천체로 FOCUSED(replace). 양 끝에서 순환한다 |
| Enter | 천체 버튼에서는 FOCUSED, TRAVEL 버튼에서는 WARPING |

키보드 접근성을 위해 HUD 레이어에 천체마다 `<button>`을 둔다(접근 가능한 이름은 "ARCHIVA — BLOG"). 순서는 about → projects → blog → playground다.
HUD가 열리거나 선택한 천체가 바뀌면 키보드 포커스를 HUD 제목으로 옮긴다. HUD가 닫히면 메인 화면 루트로 옮긴다.

### 4.6 HUD 레이아웃
가로/세로 배치는 화면 비(너비/높이) 1을 기준으로 한다. 1보다 크면 아래의 데스크톱 배치, 1 이하이면 모바일 세로 화면 배치를 사용하며, 카메라도 같은 기준으로 HUD 자리를 남긴다.

**데스크톱**: 레퍼런스처럼 정보 패널은 왼쪽에, TRAVEL은 하단 중앙에 둔다.
```
┌──────────────────────────────────────────────────────────────────┐
│                         LOONACIAVERSE                            │
│                       SELECT DESTINATION                         │
│                                                                  │
│  ARCHIVA ◉                                          ARTIFEX      │
│  ── BLOG ─────────────────                         PROJECTS     │
│  기록과 회고, 기술 메모               ( 줌인된 행성 +               │
│  POSTS               24                홀로그램 링 )              │
│  LAST TRANSMISSION   2026-10-20                                  │
│  TOP TAGS            UNITY · BLAZOR                              │
│                                                                  │
│  LATEST                                                          │
│  ┌──────────────┐                                                │
│  │ 최신 글 카드   │                                                │
│  └──────────────┘                                                │
│                          [   TRAVEL   ]                    [ ✕ ] │
└──────────────────────────────────────────────────────────────────┘
```
**모바일 세로 화면**: 천체를 화면 위쪽 중앙에 두고, HUD는 하단 시트(화면 높이 45% 이내)로 띄운다. 시트 하단에 TRAVEL이 꽉 찬 너비로 있다. 시트 바깥을 탭하면 닫힌다.

### 4.7 HUD 내용
모든 수치는 빌드할 때 `universe.json`에 계산되어 들어간다. 초안은 제외한다.

| 천체 | 표시 항목 |
|---|---|
| about | `CLASS` 직함, `PRIMARY STACK` 주 스택, `COMMS` 이메일·GitHub·LinkedIn 아이콘 링크 |
| projects | `PROJECTS` 개수, `PRIMARY STACK` 프로젝트 stack 빈도 상위 3개, `FEATURED` 대표 프로젝트 카드(썸네일, 제목). 카드를 누르면 `/projects/{slug}`로 바로 간다 |
| blog | `POSTS` 개수, `LAST TRANSMISSION` 최신 글 날짜, `TOP TAGS` 상위 3개, `LATEST` 최신 글 카드. 카드를 누르면 `/blog/{slug}`로 바로 간다 |
| playground | `STATUS` SIGNAL UNCONFIRMED, `COMING SOON`. TRAVEL은 비활성(잠금 색) |

모든 천체 HUD 맨 위에는 세계관 이름, 부제, `tagline`(한국어 한 줄)이 있다.

## 5. 목록형 홈 (대체 경로)
- **조건**: WebGL2를 쓸 수 없거나(씬 `init`이 false 반환) `prefers-reduced-motion: reduce`일 때.
- `/`에서 3D 대신 HUD 스타일 카드 4개를 세로로 보여 준다. 카드에는 4.7과 같은 내용과 링크가 들어간다. Playground 카드는 비활성이다.
- 워프 연출이 없고 링크로 바로 이동한다.

## 6. 상세 페이지 공통
- **상단 바**:
  - 왼쪽에 `LOONACIAVERSE` 로고가 있다. 누르면 `/?focus={현재 섹션 id}`로 이동해 우주로 돌아가고, 직전 행성에 포커스된 채 시작한다.
  - 오른쪽에 `PROJECTS · BLOG · ABOUT` 링크가 있다. 현재 섹션은 HUD 색 밑줄로 표시한다.
  - 블로그 페이지에서는 RSS 아이콘(`/feed.xml`)을 더한다.
  - 모바일에서도 같은 구성을 축소해서 쓴다(햄버거 메뉴 없음).
- **톤**:
  - 다크 전용이다.
  - 배경은 `--color-bg` 위에 정적인 CSS 별 점 몇 겹을 둔다. 3D는 쓰지 않는다.
  - HUD 스타일(1px 선, 모서리 브래킷, 대문자 레터스페이싱)은 페이지 헤더, 메타 정보, 섹션 라벨에만 쓴다.
- **본문**:
  - 폭은 최대 72ch, 줄 간격은 1.75다.
  - 코드 블록은 highlight.js로 하이라이트하고 테마는 토큰 색에 맞춘다.
  - 이미지는 본문 폭에 맞춘다. YouTube는 16:9 반응형으로 임베드하고 `youtube-nocookie.com`을 쓴다.
- **페이지 헤더**: 작은 HUD 라벨(세계관 이름, 예: `ARCHIVA`) 아래에 큰 제목을 둔다.
- **폰트 규칙**: 한글이 들어갈 수 있는 텍스트(제목, 본문, 요약, 태그)는 반드시 Pretendard로 쓴다. Michroma와 Rajdhani는 영어 고정 라벨과 숫자에만 쓴다. 이유: 두 폰트에는 한글 글리프가 없다.

## 7. 페이지별
- **블로그 목록** `/blog`:
  - 위쪽에 태그 칩 목록이 있다(누르면 `/blog/tags/{tag}`).
  - 글 항목에는 날짜(Rajdhani), 제목, 요약, 태그를 보여 준다.
  - 날짜 내림차순이다. Debug 빌드의 초안에는 `DRAFT` 배지를 붙인다.
- **태그 목록** `/blog/tags/{tag}`: 헤더는 `TAG` 라벨과 태그 이름이다. 블로그 목록과 같은 형식에 "전체 글" 링크를 더한다.
- **글 상세** `/blog/{slug}`:
  - 메타 줄은 `PUBLISHED yyyy-MM-dd`이고, 수정된 글이면 `· UPDATED yyyy-MM-dd`를 붙인다.
  - 제목, 태그, 커버 이미지(있으면) 뒤에 본문이 온다.
  - 목차(`CONTENTS`)는 데스크톱에서 오른쪽에 sticky로, 모바일에서는 본문 위에 접이식으로 둔다.
  - 하단에 `← ALL POSTS` 링크를 둔다.
- **프로젝트 목록** `/projects`:
  - 카드 그리드로 보여 준다(썸네일 16:9, 제목, 기간, 역할, 스택 칩, 요약).
  - `featured` 카드에는 `FEATURED` 배지를 붙이고 맨 앞에 둔다.
- **프로젝트 상세** `/projects/{slug}`:
  - 썸네일 히어로, 제목, HUD 스타일 메타 표(`PERIOD`, `ROLE`, `STACK`, `LINKS`) 뒤에 본문이 온다.
  - 본문은 개요, 문제와 해결, 결과, 미디어 순으로 쓰는 것을 권장한다.
- **About** `/about`: 이름, 직함, 스택 칩, 소개 본문, `COMMS` 링크(이메일, GitHub, LinkedIn) 순이다.
- **404**: `LOST IN SPACE` 제목, 짧은 한국어 안내, `RETURN TO ORBIT` 버튼(`/`)으로 구성한다.
- **콘텐츠 로드 실패**: 그 영역에 `SIGNAL LOST`, 짧은 한국어 안내, `RETRY` 버튼을 보여 준다.

## 8. 디자인 토큰
초기값이며 시안을 보며 조정한다. 토큰은 `:root`에 한 벌만 둔다(다크 전용).

| 토큰 | 값 | 용도 |
|---|---|---|
| `--color-bg` | `#05070d` | 배경 |
| `--color-surface` | `rgba(11, 18, 32, 0.8)` | HUD 패널, 카드 |
| `--color-hud` | `#5fd4ff` | HUD 강조, 링크, 게이지 |
| `--color-hud-dim` | `rgba(95, 212, 255, 0.35)` | HUD 선, 테두리 |
| `--color-text` | `#e6eef7` | 본문 |
| `--color-text-muted` | `#8a9bb0` | 메타, 요약 |
| `--color-locked` | `#ffb347` | 잠금, DRAFT |
| `--color-danger` | `#ff5f6d` | SIGNAL LOST |
| `--font-display` | Michroma | 사이트 로고, 큰 영어 타이틀 |
| `--font-hud` | Rajdhani | HUD 라벨, 수치, 날짜 |
| `--font-body` | Pretendard | 한글이 들어가는 모든 텍스트 |

HUD 라벨은 대문자로 쓰고 `letter-spacing: 0.12em`을 준다.

## 9. 렌더 품질 단계
| 항목 | 데스크톱 | 모바일(터치, 좁은 화면) |
|---|---|---|
| 픽셀 비율 | `min(devicePixelRatio, 2)` | `min(devicePixelRatio, 1.5)` |
| 블룸 후처리 | 켬 | 끔 또는 절반 해상도 |
| 별 개수 | 많음 | 절반 이하 |

탭이 숨겨지면(`visibilitychange`) 렌더 루프를 멈춘다. 목표 프레임은 PRD 제약(데스크톱 60fps, 중급 모바일 30fps)이다.

## 10. HUD 카피
`SELECT DESTINATION`, `TRAVEL`, `CLOSE`, `COMING SOON`, `SIGNAL UNCONFIRMED`, `NAVIGATION SYSTEM BOOT`, `LOST IN SPACE`, `RETURN TO ORBIT`, `SIGNAL LOST`, `RETRY`, `POSTS`, `LAST TRANSMISSION`, `TOP TAGS`, `LATEST`, `PROJECTS`, `PRIMARY STACK`, `FEATURED`, `CLASS`, `COMMS`, `STATUS`, `CONTENTS`, `PUBLISHED`, `UPDATED`, `PERIOD`, `ROLE`, `STACK`, `LINKS`, `DRAFT`, `ALL POSTS`, `TAG`
