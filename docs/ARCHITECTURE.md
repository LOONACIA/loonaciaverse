# 아키텍처

> 스텝 description의 "읽어야 할 파일"로 지정되는 문서입니다. 실제 코드와 어긋나면 코드가 아니라 이 문서를 고치세요.
> 결정의 이유는 [ADR.md](ADR.md)에 있습니다.

## 개요
서버가 없는 정적 사이트다. 빌드할 때 Markdown 콘텐츠를 JSON·HTML로 바꾸고, Three.js 씬을 번들링하고, Blazor WebAssembly 앱을 게시한다. 브라우저에서는 Blazor가 라우팅, HUD, 상세 페이지를 맡고, Three.js 씬은 홈에서만 렌더러로 동작한다.

## 디렉토리 구조
```
loonaciaverse/
├─ global.json                       # .NET 10 SDK 고정
├─ Loonaciaverse.slnx                # .NET 10 SDK 기본 솔루션 형식
├─ src/
│  ├─ Loonaciaverse.Core/            # 콘텐츠 모델(record), IContentService. 외부 의존성 없음
│  └─ Loonaciaverse.Web/             # Blazor WASM Standalone
│     ├─ Pages/                      # 라우트 컴포넌트 (Home, BlogList, BlogPost, BlogSeries, ProjectList, ProjectDetail, About, NotFound)
│     ├─ Components/
│     │  ├─ StarSystem.razor         # 씬 수명주기, URL 포커스, 키보드 입력, HUD·목록형 홈 전환
│     │  ├─ HudPanel.razor           # HUD(데스크톱 콜아웃, 모바일 하단 시트), 닫기·TRAVEL 버튼
│     │  ├─ BodyInfo.razor           # HUD와 목록형 홈이 공유하는 천체 정보
│     │  ├─ ListHome.razor           # 씬을 쓸 수 없을 때의 카드 목록
│     │  └─ ...                      # 상단 바, 콘텐츠 카드 등
│     ├─ Services/                   # HttpContentService, SceneInterop
│     └─ wwwroot/
│        ├─ index.html               # 부팅 화면 마크업, 정적 링크, 공통 OG
│        ├─ og-image.png             # 1200×630 기본 OG 이미지(임시, 사이트 주인이 교체)
│        ├─ staticwebapp.config.json
│        ├─ css/  fonts/
│        ├─ content/                 # [빌드 산출물] ContentBuilder 출력
│        ├─ js/                      # [빌드 산출물] Vite 출력 (scene.js, highlight.js, toc.js)
│        └─ feed.xml, sitemap.xml, robots.txt   # [빌드 산출물]
├─ tools/
│  ├─ Loonaciaverse.ContentBuilder/  # 콘솔: content/ → wwwroot/content/ (Markdig, YamlDotNet)
│  └─ Loonaciaverse.BudgetCheck/     # 콘솔: publish 결과의 홈 전송량 검사
├─ scene/                            # npm 프로젝트: TypeScript + Vite + Vitest
│  ├─ src/scene/
│  │  ├─ scene.ts                   # 공개 계약, 렌더 루프, 입력, 블룸, 수명주기
│  │  ├─ types.ts                   # UniverseDto, BodyDto, DotNetCallbacks
│  │  ├─ system.ts                  # 천체·고리·별 필드·성운·유성 생성, 자전, 셰이더 시간, 별 궤적
│  │  ├─ materials.ts               # 천체 표면(surface별)·대기·구름층·고리·코로나·별 필드·성운의 절차적 셰이더
│  │  ├─ meteor.ts                  # 가끔 배경을 가로지르는 유성, 대기·비행 주기
│  │  ├─ labels.ts                  # CSS2D 이름표, 클릭·호버, 표시 상태
│  │  ├─ ring.ts                    # 포커스 홀로그램 링(궤도면)·궤도선·궤도 그리드, 회전·페이드·펼침
│  │  ├─ orbit.ts                   # 공전 위치·시간·정지 조건
│  │  ├─ camera.ts                  # 화면 비별 카메라 구도, 보간·트윈
│  │  ├─ warp.ts                    # 900ms 워프 타임라인
│  │  ├─ quality.ts                 # 품질 단계 선택
│  │  └─ *.test.ts                  # Vitest 테스트
│  ├─ src/highlight.ts               # highlight.js 래퍼 (필요한 언어만 등록)
│  ├─ src/toc.ts                     # 목차 클릭 스크롤
│  └─ sandbox/                       # index.html, main.ts: Blazor 없이 씬을 확인하는 Vite 페이지
├─ content/
│  ├─ site.yml                       # 사이트 제목, baseUrl, 작성자
│  ├─ universe.yml                   # 천체 정의
│  ├─ series.yml                     # 블로그 시리즈 정의(선택)
│  ├─ about.md
│  ├─ posts/{slug}/index.md          # 이미지 등은 같은 폴더에
│  └─ projects/{slug}/index.md
├─ tests/
│  ├─ Loonaciaverse.ContentBuilder.Tests/   # xUnit
│  ├─ Loonaciaverse.BudgetCheck.Tests/      # xUnit
│  └─ Loonaciaverse.Web.Tests/              # bUnit
├─ .github/workflows/
│  └─ ci.yml                         # PR 테스트, main 테스트·publish·예산 검사·SWA 배포
├─ artifacts/                        # [빌드 산출물] publish 결과(artifacts/publish/)
└─ docs/
```
`[빌드 산출물]` 경로는 모두 `.gitignore`에 넣는다.
저장소 루트에서 `npm run dev --prefix scene`으로 sandbox를 실행한다. 천체별 FOCUS·HIGHLIGHT·WARP, CLEAR와 immediate 옵션으로 씬을 확인하며, 워프 완료 1초 뒤 씬을 다시 초기화한다.

## 레이어 규칙
- 의존 방향: `Web → Core`, `ContentBuilder → Core`, 테스트 → 각 대상. Core는 아무것도 참조하지 않는다. 이유: 나중에 Web App 서버를 추가할 때 서버도 Core만 참조하면 된다.
- Markdig, YamlDotNet은 ContentBuilder에만 둔다. Web(WASM)에 넣지 않는다. 이유: WASM 번들 크기 예산(PRD 제약).
- Web의 페이지와 컴포넌트는 콘텐츠를 `IContentService`로만 읽는다. `HttpClient`로 `content/` 경로를 직접 요청하지 않는다. 이유: 프리렌더링이나 Web App으로 옮길 때 구현체만 바꾸기 위해서다(ADR-004).
- 서비스 등록은 `Program`의 정적 메서드 `ConfigureServices` 한 곳에 모은다. 이유: BlazorWasmPreRendering.Build가 이 형태를 요구한다. 도입할 때 README에서 정확한 시그니처를 확인한다.
- Three.js 코드는 `scene/`에만 있다. C# 쪽은 `SceneInterop` 하나만 씬 모듈을 import하고 호출한다. 페이지가 `IJSRuntime`으로 씬 함수를 직접 부르지 않는다.
- HUD는 Blazor가 그린다. 예외로 천체 이름표는 매 프레임 3D 위치를 따라가야 하므로 씬 모듈이 CSS2DRenderer로 관리한다(ADR-003).
- "어떤 천체가 선택됐는가"의 유일한 원천은 URL(`?focus=`)이고, 이를 홈의 `StarSystem` 컴포넌트가 해석해 씬에 명령한다. 씬은 렌더링에 필요한 포커스·호버·강조·애니메이션 상태를 보관하며, C#에는 클릭만 알린다. 호버와 그에 따른 이름표 표시·공전 정지는 씬 내부에서 처리한다.

## 콘텐츠 모델 (Core)
초안이며 구현하면서 조정할 수 있다. 바뀌면 이 절을 고친다.

```csharp
public interface IContentService
{
    Task<Universe> GetUniverseAsync();
    Task<IReadOnlyList<PostSummary>> GetPostsAsync();
    Task<PostDetail?> GetPostAsync(string slug);
    Task<SeriesDetail?> GetSeriesAsync(string slug);
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync();
    Task<ProjectDetail?> GetProjectAsync(string slug);
    Task<AboutProfile> GetAboutAsync();
}
```
- `Universe`: 사이트 정보(`site.yml`), 천체 목록(`universe.yml`), 천체별 HUD 스탯(빌드할 때 계산).
- 글 front matter: `title`, `slug`, `date`, `updated?`, `summary`, `tags`, `draft`, `cover?`, `series?`
- 시리즈(`series.yml`, 없어도 된다): `slug`, `title`, `summary`. 글은 front matter `series`로 시리즈 하나에만 속한다. 편 순서는 `date` 오름차순이고 같으면 slug 순이다. 빌드에 포함된 글로만 시리즈를 구성하며, 예정 편은 두지 않는다.
- 프로젝트 front matter: `title`, `slug`, `summary`, `period`, `role`, `stack`, `links`(github, store, video 등), `thumbnail`, `featured`, `order`, `draft`
- `about.md` front matter: `name`, `title`(직함), `stack`, `email`, `github`, `linkedin`
- 천체(`universe.yml`): `id`(`about`|`projects`|`blog`|`playground`), `name`(세계관 이름), `subtitle`(카테고리), `route`, `locked`, `visual`(셰이더 파라미터: 색상, 노이즈 스케일, 대기 색, 글로우 세기, 행성 `surface`(`terran`|`gas`|`desert`, 항성은 생략)), `orbit`(반지름, 주기, 위상)

## 빌드 산출물 (`wwwroot/content/`)
| 파일 | 내용 |
|---|---|
| `universe.json` | 사이트 정보, 천체 정의, HUD 스탯 |
| `posts.json` | 글 요약 목록(날짜 내림차순) |
| `posts/{slug}.json` | 글 메타, 본문 HTML, 목차, 시리즈 정보(시리즈에 속한 글만: 시리즈 slug·제목, 편 순서대로 편 slug·제목·초안 여부) |
| `series/{slug}.json` | 시리즈 slug·제목·설명, 편 순서대로 글 요약 목록. 빌드에 포함된 편이 하나 이상인 시리즈만 |
| `posts/{slug}/*` | 글 폴더의 이미지 복사본 |
| `projects.json`, `projects/{slug}.json`, `projects/{slug}/*` | 프로젝트, 글과 같은 구조 |
| `about.json` | 프로필과 본문 HTML |

ContentBuilder는 그 밖에 `wwwroot/feed.xml`, `wwwroot/sitemap.xml`, `wwwroot/robots.txt`도 만든다. 절대 URL은 `site.yml`의 `baseUrl`로 만든다.

## 데이터 흐름

**빌드**
```
dotnet build src/Loonaciaverse.Web
 ├─ (MSBuild 타깃) ContentBuilder 실행: content/ → wwwroot/content/, feed.xml, sitemap.xml, robots.txt
 │    Debug는 --include-drafts, Release는 초안 제외
 ├─ (MSBuild 타깃) scene/에서 npm run build → wwwroot/js/
 └─ Blazor WASM 컴파일
```
- ContentBuilder는 Web 프로젝트의 `ProjectReference`(`ReferenceOutputAssembly="false"`)로 먼저 빌드한 뒤, 빌드된 dll을 `Exec`로 실행한다. `dotnet run`을 쓰지 않는다. 이유: 같은 빌드 안에서 Core를 동시에 빌드하다 파일 잠금이 생긴다.
- `ProjectReference`의 `GlobalPropertiesToRemove`로 Web의 TargetFramework, RuntimeIdentifier와 SelfContained 설정을 제거하고, `SkipGetTargetFrameworkProperties`로 단일 프레임워크 빌드 도구의 프레임워크 협상을 생략한다. `OutputItemType`으로 실행할 DLL 경로를 받는다.
- 두 타깃 모두 `Inputs`/`Outputs`로 증분 빌드한다. ContentBuilder 타깃은 `content/**`, ContentBuilder DLL, 입력 목록과 Web csproj를 `Inputs`, `$(IntermediateOutputPath)content-builder.stamp`를 `Outputs`로 사용한다. `$(BaseIntermediateOutputPath)content-builder.inputs`에 파일 목록과 Configuration을 기록해 파일 삭제와 Debug/Release 전환도 감지하며, 필수 산출물이 없으면 다시 생성한다. `scene/node_modules`가 없으면 `npm ci`를 먼저 실행한다.
- 정적 웹 자산 수집 전에 생성 파일의 `Content` 항목을 갱신해 첫 빌드와 publish에도 포함한다. 변환 실패는 빌드를 실패시키며 오류는 빌드 출력에 표시된다.
- Web csproj에 `content/**`와 `scene/src/**`를 `Watch` 항목으로 추가해 `dotnet watch`가 변경을 감지하게 한다.
- 글·프로젝트·About 본문에 `![제목](https://www.youtube.com/watch?v=ID)` 또는 `![제목](https://youtu.be/ID)`를 쓰면 ContentBuilder의 `MediaEmbeds`가 `https://www.youtube-nocookie.com/embed/ID`를 가리키는 iframe(`class="youtube"`)으로 변환한다. 각 빌더는 `UseNoCookieMediaLinks()`를 `UseAdvancedExtensions()`보다 먼저 등록하며, `app.css`의 `.prose iframe.youtube`이 너비 100%, `aspect-ratio: 16 / 9`로 반응형 표시를 맞춘다.

**런타임: 홈**
```
index.html(부팅 화면) → Blazor 부팅 → Home 페이지
 → IContentService.GetUniverseAsync()
 → StarSystem(Universe): 씬 호스트와 Blazor HUD 관리
 → 첫 렌더 후 SceneInterop.InitAsync(container, universe)
    # WebGL2 불가·reduced-motion·씬 모듈 import 실패 시 ListHome
 ← 씬 OnBodyClicked(id | null) → StarSystem → NavigationManager로 ?focus 갱신 또는 닫기
 → URL ?focus 해석 → HudPanel 표시 + SceneInterop.FocusAsync(id, immediate) / ClearFocusAsync()
 → 천체 버튼 키보드 포커스 → SceneInterop.HighlightAsync(id | null)
 → TRAVEL → SceneInterop.WarpAsync(id)가 끝나면 NavigateTo(route)
 → StarSystem Dispose → SceneInterop.DisposeAsync()
```
- `Home`은 콘텐츠 로드와 페이지 메타데이터를 맡고, `StarSystem`은 씬·HUD·키보드 입력·목록형 fallback을 맡는다. `HudPanel`과 `ListHome`은 `BodyInfo`를 공유하므로 같은 천체 정보와 링크를 보여 준다.
- `SceneInterop`은 DI에 등록하지 않고 `StarSystem`이 `new SceneInterop(JS)`로 생성한다. 씬 컨테이너와 JS 모듈·`DotNetObjectReference`의 수명을 컴포넌트에 맞추고, 홈을 벗어날 때 이벤트 구독과 씬 리소스를 함께 해제하기 위해서다.
- `StarSystem`은 현재 URL의 `focus`를 천체 목록과 대조하며, 알 수 없는 값은 `/`로 replace한다. 최초 적용은 `immediate: true`로 직접 진입 시 줌인 트윈을 생략하고, 이후 URL 변경에는 포커스·해제 트윈을 적용한다.
- OVERVIEW에서 천체를 선택하면 포커스 URL을 push하고 `NavigationOptions.HistoryEntryState`에 `loonaciaverse:home-focus` 마커를 넣는다. 다른 천체로 바꾸면 현재 항목을 replace하며 기존 `HistoryEntryState`를 보존한다. BACK·Esc·빈 공간 클릭으로 닫을 때 현재 항목에 마커가 있으면 `history.back()`을 호출하고, 직접 진입처럼 마커가 없으면 `/`로 replace한다. 브라우저 뒤로가기도 URL 변경을 통해 씬과 HUD에 반영한다.
- TRAVEL은 잠기지 않고 라우트가 있는 천체에서만 동작한다. 워프 중에는 HUD를 숨기고 입력과 URL 변경의 반영을 차단하며, 연출이 실패해도 상세 페이지로 이동한다. 목록형 홈은 워프 없이 일반 링크로 이동한다.

**런타임: 상세**
```
라우트 → 페이지 → IContentService(HttpContentService가 wwwroot/content/*.json을 fetch)
 → MarkupString으로 본문 HTML 렌더 → OnAfterRenderAsync에서 highlight.js 적용
```

## 씬 모듈 계약 (`scene/src/scene/scene.ts`)
```ts
export function init(container: HTMLElement, universe: UniverseDto, callbacks: DotNetCallbacks): boolean; // WebGL2 불가 또는 reduced-motion이면 false
export function focus(bodyId: string, immediate: boolean): void;
export function clearFocus(): void;
export function highlight(bodyId: string | null): void;
export function warp(bodyId: string): Promise<void>; // 900ms 타이머로 1초 이내에 resolve
export function dispose(): void;
```
- `init`은 이전 씬을 dispose한 뒤 컨테이너 안에 캔버스와 CSS2D 이름표 레이어를 만든다. 렌더러 생성 실패도 `false`로 반환한다. `ResizeObserver`로 크기를 맞추고, `visibilitychange`로 숨겨진 탭의 렌더 루프를 멈춘다.
- `focus`는 `immediate`가 true이면 즉시, false이면 0.8초 트윈으로 천체에 접근한다. `clearFocus`는 0.8초 트윈으로 전체 구도로 돌아간다. `highlight`는 키보드로 강조할 천체를 지정하며 `null`로 해제한다. 호버·키보드 강조 중에는 공전을 멈추지만 자전과 셰이더 시간은 계속 흐른다. 포커스 중에는 공전을 멈추지 않고 카메라가 매 프레임 천체를 따라가며, 포커스한 천체를 호버·강조해도 공전을 멈추지 않는다.
- C# 콜백은 `SceneInterop`의 `[JSInvokable] public Task OnBodyClicked(string? id)` 하나다. `null`은 빈 공간 클릭이며, 이름표 클릭도 같은 콜백으로 전달한다. 호버 콜백은 없고 씬 내부에서만 처리한다.
- `warp`는 카메라 돌진·별 궤적·배경색 페이드를 실행한다. 완료는 `requestAnimationFrame`에 의존하지 않는 `setTimeout(..., 900)`으로 resolve하므로 렌더 루프가 멈춰도 완료를 기다릴 수 있다. 워프를 시작하면 입력과 포커스·강조 명령을 무시하고 이름표와 링을 숨긴다. 반복 호출은 진행 중인 Promise를 반환하며, 씬이 없거나 id가 유효하지 않으면 즉시 resolve한다.
- `dispose`는 렌더 루프·이벤트·observer·DOM·GPU 리소스·블룸 패스를 해제하고, 진행 중인 워프 타이머를 취소하며 Promise도 resolve한다. 다시 초기화하거나 여러 번 해제할 수 있다.
- Three.js에 의존하지 않는 순수 로직 모듈은 `orbit.ts`(공전 위치, 공전 시계, 정지 조건), `camera.ts`(전체·포커스 구도, easing, 보간, 트윈), `warp.ts`(돌진·궤적·페이드 타임라인), `quality.ts`(품질 단계)다. `labels.ts`의 표시 상태와 `ring.ts`의 불투명도 목표·페이드와 궤도선 펼침 계산도 순수 함수로 검증하고, `meteor.ts`의 대기·비행 주기는 `Math.random`을 고정해 검증한다. URL↔포커스와 history 규칙은 `StarSystem`의 책임이다.

## 횡단 관심사
- **에러 처리**:
  - 콘텐츠 fetch가 실패하면 HUD 스타일의 "SIGNAL LOST" 안내와 재시도 버튼을 보여 준다.
  - 없는 라우트나 slug는 테마형 404(LOST IN SPACE)로 보낸다. SWA 대체 경로 때문에 HTTP 상태는 200(soft 404)이다. 이는 알려진 한계다(ADR-001).
  - Blazor 기본 `#blazor-error-ui`는 테마에 맞게 스타일만 바꾼다.
- **설정**:
  - 사이트 설정은 `content/site.yml`, 천체는 `content/universe.yml`에 둔다. 코드에 하드코딩하지 않는다. 예외로 정적이어야 하는 `index.html`의 공통 OG 값은 같은 값을 직접 쓰고, `OpenGraphTests`가 일치를 검사한다(아래 SEO 참고).
  - 빌드 설정은 MSBuild 속성으로 둔다(`Configuration`에 따라 초안 포함 여부 결정).
- **SEO**:
  - 페이지별 `<PageTitle>`, `HeadContent`의 description과 canonical은 런타임에 설정한다.
  - `index.html`에는 사이트 공통 OG 태그와 기본 OG 이미지를 둔다.
  - 공통 OG 값은 `content/site.yml`과 같아야 한다. `og:site_name`, `og:title`, `og:image:alt`는 `title`, `og:description`은 `description`을 쓴다. `og:url`은 `baseUrl` 끝의 `/`를 제거한 뒤 `/`를 붙이고, `og:image`는 같은 기준 URL에 `/og-image.png`를 붙인다. 사이트 설정을 바꿀 때 `index.html`도 함께 고친다.
  - `og:type`은 `website`, `og:locale`은 `ko_KR`, `twitter:card`는 `summary_large_image`다. 기본 이미지는 `wwwroot/og-image.png`의 1200×630 PNG이며, 사이트 주인이 같은 크기의 최종 이미지로 교체한다. `og:image:width`, `og:image:height`에도 각각 `1200`, `630`을 쓴다.
  - `OpenGraphTests`는 `site.yml`에서 빌드한 `universe.json`의 `SiteInfo`와 OG 값의 일치, URL의 절대 HTTPS 형식, 로컬 PNG의 존재·크기, 고정 메타 값과 태그 중복 여부를 검사한다. 일반 description과 canonical은 `index.html`에 넣지 않고 런타임 페이지 메타데이터에 맡기는지도 검사한다.
- **캐시**: `staticwebapp.config.json`에서 `/content/*`에 `Cache-Control: no-cache`를 건다(ETag로 재검증). 배포 직후 옛 콘텐츠가 보이지 않게 하기 위해서다.
- **SPA 라우팅**: `staticwebapp.config.json`의 `navigationFallback.rewrite`를 `/index.html`로 한다. `exclude`는 `/_framework/*`, `/content/*`, `/js/*`, `/css/*`, `/fonts/*`, `*.{css,js,json,wasm,xml,txt,png,jpg,jpeg,gif,svg,webp,ico,woff2}`로 설정해, 해당 정적 파일 경로에 없는 파일은 진짜 404가 나게 한다.
- **크기**: Web csproj에 `InvariantGlobalization=true`를 켠다. 날짜는 `yyyy-MM-dd` 형식 문자열로 직접 포맷한다. 한국어 표시에는 영향이 없다.
- **폰트**: woff2로 `wwwroot/fonts/`에 자체 호스팅한다(Michroma, Rajdhani, Pretendard dynamic subset). 외부 폰트 CDN에 의존하지 않는다.
- **로깅·관측**: MVP에는 없다(분석 도구 범위 밖).

## 테스트
| 대상 | 도구 | 명령 |
|---|---|---|
| ContentBuilder (front matter 파싱, 검증 실패, 인덱스·RSS·sitemap·TOC 생성, 초안 제외, YouTube 임베드, 시리즈 검증·편 순서·산출물) | xUnit | `dotnet test` |
| Web 컴포넌트·SceneInterop (URL↔포커스, history·닫기, 키보드 순환·DOM 포커스, TRAVEL·실패 시 이동, fallback·수명주기, HUD 스탯·잠금, 상단 바, 목록·상세 렌더, 시리즈 박스·이전/다음·시리즈 페이지, 404), OG 일치·이미지 및 SWA 설정 검사 | xUnit + bUnit + 가짜 `IContentService`·JS interop | `dotnet test` |
| 전송량 예산 검사 (집계·제외 대상, `.br` 우선, Pretendard 청크 선택, 필수 자산, 한도 경계, 종료 코드) | xUnit | `dotnet test` |
| 씬 순수 로직 (공전 위치·시간·정지 조건, 카메라 구도·보간·트윈, 워프 타임라인, 품질 단계, 이름표 상태·링 페이드·궤도선 펼침, 유성 주기) | Vitest | `npm test --prefix scene` |
| 씬 구성·계약 (천체·별·셰이더 uniforms, 이름표·링, 클릭·호버·강조, 워프 타이머·입력 차단, resize·블룸·dispose) | Vitest + happy-dom, 렌더러·composer 모의 객체 | `npm test --prefix scene` |
| 3D 렌더링 결과, 셰이더 표현, 연출 | 수동 확인(sandbox, 실제 사이트) | `npm run dev --prefix scene` |

## CI/CD (`.github/workflows/`)
- `.github/workflows/ci.yml`은 main 대상 PR과 main 푸시에서 실행한다. 두 job 모두 `ubuntu-latest`에서 .NET 10과 Node.js 22를 준비하고 `npm ci --prefix scene`으로 의존성을 설치한다.
- **`test` job (PR·main 푸시)**: `dotnet test`, `npm test --prefix scene`을 실행한다. PR에서는 이 job만 실행한다.
- **`deploy` job (main 푸시)**: `needs: test`로 테스트 통과를 기다리고 `if: github.event_name == 'push'`로 배포를 제한한다.
  1. `dotnet publish src/Loonaciaverse.Web -c Release -o artifacts/publish`로 게시한다.
  2. `dotnet run --project tools/Loonaciaverse.BudgetCheck -- artifacts/publish/wwwroot`로 전송량 예산(PRD 제약)을 검사한다. 실패하면 배포하지 않는다.
  3. `Azure/static-web-apps-deploy@v1`로 배포한다(`action: upload`, `skip_app_build: true`, `app_location: artifacts/publish/wwwroot`). 배포 토큰은 저장소 secret `AZURE_STATIC_WEB_APPS_API_TOKEN`에서 읽는다.
- SWA PR 미리보기 환경은 쓰지 않는다.

### 홈 전송량 예산
- `Loonaciaverse.BudgetCheck`는 publish 결과의 `wwwroot`를 입력받아 홈 첫 방문 전송량을 계산한다. 한도는 3,000,000바이트(십진수 3MB)이며, 한도와 같아도 통과한다.
- 집계 대상은 `index.html`, `css/app.css`, `js/scene.js`, `content/universe.json`, Pretendard CSS(`fonts/pretendard/pretendardvariable-dynamic-subset.css`), 존재하는 `Loonaciaverse.Web.styles.css`, `_framework/` 아래의 모든 원본 파일(.NET 런타임·앱 어셈블리 등), `fonts/michroma/`와 `fonts/rajdhani/` 바로 아래의 모든 `.woff2`, 선택된 Pretendard 청크다.
- 각 원본 파일에 `.br` 사본이 있으면 그 크기를 우선하고, 없으면 원본 크기를 쓴다. `.gz`로 대체하지 않는다. `_framework/`를 순회할 때 `.br`·`.gz` 사본은 독립 항목으로 더하지 않으며, 같은 자산 경로는 한 번만 센다.
- Pretendard는 `universe.json`의 중첩 객체·배열을 포함한 모든 문자열 값에서 Unicode 코드 포인트를 모은 뒤, CSS의 `@font-face`별 `unicode-range`와 겹치는 청크만 센다. JSON 속성 이름은 대상이 아니다. 청크 URL은 CSS 파일 위치를 기준으로 해석한다.
- 상세 페이지용 `js/highlight.js`, `js/toc.js`, 글·프로젝트·About JSON과 이미지, `og-image.png`, 폰트 라이선스 등 위 집계 대상 밖의 파일은 제외한다. 실제 브라우저 요청을 기록하는 도구가 아니므로 배포 후 개발자 도구에서도 전송량을 확인한다.
- `index.html`, `css/app.css`, `js/scene.js`, `content/universe.json`, Pretendard CSS, `_framework/` 아래 최소 한 개의 `.wasm`, 선택된 Pretendard 청크는 필수다. 필수 원본이 없으면 `.br` 사본만 있어도 실패한다.
- 파일별 크기를 큰 순서로 출력하고 합계를 표시한다. 종료 코드는 정상·예산 이내이면 `0`, 예산 초과·필수 자산 누락·읽기/JSON 오류이면 `1`, 인수 개수 오류나 입력 디렉토리 부재이면 `2`다.

저장소 루트에서 로컬로 재현한다.

```sh
dotnet publish src/Loonaciaverse.Web -c Release -o artifacts/publish
dotnet run --project tools/Loonaciaverse.BudgetCheck -- artifacts/publish/wwwroot
```

로컬에서는 기존 `artifacts/publish` 폴더를 지우고 다시 publish한다. 이전 publish의 지문 붙은 파일이 남으면 `_framework/` 집계에 포함되어 합계가 부풀 수 있다.
