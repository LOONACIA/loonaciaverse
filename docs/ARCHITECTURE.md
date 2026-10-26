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
│     ├─ Pages/                      # 라우트 컴포넌트 (Home, BlogList, BlogPost, ProjectList, ProjectDetail, About, NotFound)
│     ├─ Components/                 # HUD, 상단 바, 목록형 홈, 카드 등
│     ├─ Services/                   # HttpContentService, SceneInterop
│     └─ wwwroot/
│        ├─ index.html               # 부팅 화면 마크업, 정적 링크, 공통 OG
│        ├─ staticwebapp.config.json
│        ├─ css/  fonts/
│        ├─ content/                 # [빌드 산출물] ContentBuilder 출력
│        ├─ js/                      # [빌드 산출물] Vite 출력 (scene.js, highlight.js, toc.js)
│        └─ feed.xml, sitemap.xml, robots.txt   # [빌드 산출물]
├─ tools/
│  └─ Loonaciaverse.ContentBuilder/  # 콘솔: content/ → wwwroot/content/ (Markdig, YamlDotNet)
├─ scene/                            # npm 프로젝트: TypeScript + Vite + Vitest
│  ├─ src/scene/                     # Three.js 씬 (진입점 scene.ts)
│  ├─ src/highlight.ts               # highlight.js 래퍼 (필요한 언어만 등록)
│  ├─ src/toc.ts                     # 목차 클릭 스크롤
│  └─ sandbox/                       # Blazor 없이 씬만 띄우는 Vite 개발 서버 페이지
├─ content/
│  ├─ site.yml                       # 사이트 제목, baseUrl, 작성자
│  ├─ universe.yml                   # 천체 정의
│  ├─ about.md
│  ├─ posts/{slug}/index.md          # 이미지 등은 같은 폴더에
│  └─ projects/{slug}/index.md
├─ tests/
│  ├─ Loonaciaverse.ContentBuilder.Tests/   # xUnit
│  └─ Loonaciaverse.Web.Tests/              # bUnit
├─ .github/workflows/
└─ docs/
```
`[빌드 산출물]` 경로는 모두 `.gitignore`에 넣는다.

## 레이어 규칙
- 의존 방향: `Web → Core`, `ContentBuilder → Core`, 테스트 → 각 대상. Core는 아무것도 참조하지 않는다. 이유: 나중에 Web App 서버를 추가할 때 서버도 Core만 참조하면 된다.
- Markdig, YamlDotNet은 ContentBuilder에만 둔다. Web(WASM)에 넣지 않는다. 이유: WASM 번들 크기 예산(PRD 제약).
- Web의 페이지와 컴포넌트는 콘텐츠를 `IContentService`로만 읽는다. `HttpClient`로 `content/` 경로를 직접 요청하지 않는다. 이유: 프리렌더링이나 Web App으로 옮길 때 구현체만 바꾸기 위해서다(ADR-004).
- 서비스 등록은 `Program`의 정적 메서드 `ConfigureServices` 한 곳에 모은다. 이유: BlazorWasmPreRendering.Build가 이 형태를 요구한다. 도입할 때 README에서 정확한 시그니처를 확인한다.
- Three.js 코드는 `scene/`에만 있다. C# 쪽은 `SceneInterop` 하나만 씬 모듈을 import하고 호출한다. 페이지가 `IJSRuntime`으로 씬 함수를 직접 부르지 않는다.
- HUD는 Blazor가 그린다. 예외로 천체 이름표는 매 프레임 3D 위치를 따라가야 하므로 씬 모듈이 CSS2DRenderer로 관리한다(ADR-003).
- 씬은 상태를 갖지 않는 렌더러다. "어떤 천체가 선택됐는가"의 유일한 원천은 URL(`?focus=`)이고, 이를 Blazor Home 페이지가 해석해 씬에 명령한다. 씬은 클릭과 호버 이벤트만 C#으로 알린다.

## 콘텐츠 모델 (Core)
초안이며 구현하면서 조정할 수 있다. 바뀌면 이 절을 고친다.

```csharp
public interface IContentService
{
    Task<Universe> GetUniverseAsync();
    Task<IReadOnlyList<PostSummary>> GetPostsAsync();
    Task<PostDetail?> GetPostAsync(string slug);
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync();
    Task<ProjectDetail?> GetProjectAsync(string slug);
    Task<AboutProfile> GetAboutAsync();
}
```
- `Universe`: 사이트 정보(`site.yml`), 천체 목록(`universe.yml`), 천체별 HUD 스탯(빌드할 때 계산).
- 글 front matter: `title`, `slug`, `date`, `updated?`, `summary`, `tags`, `draft`, `cover?`
- 프로젝트 front matter: `title`, `slug`, `summary`, `period`, `role`, `stack`, `links`(github, store, video 등), `thumbnail`, `featured`, `order`, `draft`
- `about.md` front matter: `name`, `title`(직함), `stack`, `email`, `github`, `linkedin`
- 천체(`universe.yml`): `id`(`about`|`projects`|`blog`|`playground`), `name`(세계관 이름), `subtitle`(카테고리), `tagline`, `route`, `locked`, `visual`(셰이더 파라미터: 색상, 노이즈 스케일, 대기 색 등), `orbit`(반지름, 주기, 위상)

## 빌드 산출물 (`wwwroot/content/`)
| 파일 | 내용 |
|---|---|
| `universe.json` | 사이트 정보, 천체 정의, HUD 스탯 |
| `posts.json` | 글 요약 목록(날짜 내림차순) |
| `posts/{slug}.json` | 글 메타, 본문 HTML, 목차 |
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

**런타임: 홈**
```
index.html(부팅 화면) → Blazor 부팅 → Home 페이지
 → IContentService.GetUniverseAsync()
 → SceneInterop.InitAsync(canvas, universe)   # WebGL 불가 또는 reduced-motion이면 목록형 홈
 → URL ?focus 해석 → SceneInterop.FocusAsync(id) / ClearFocusAsync()
 ← 씬 이벤트(OnBodyClicked, OnBodyHovered) → NavigationManager로 ?focus 갱신
 → TRAVEL → SceneInterop.WarpAsync(id)가 끝나면 NavigateTo(route)
 → Home Dispose → SceneInterop.DisposeAsync()
```

**런타임: 상세**
```
라우트 → 페이지 → IContentService(HttpContentService가 wwwroot/content/*.json을 fetch)
 → MarkupString으로 본문 HTML 렌더 → OnAfterRenderAsync에서 highlight.js 적용
```

## 씬 모듈 계약 (`scene/src/scene/scene.ts`)
```ts
export function init(canvas: HTMLCanvasElement, universe: UniverseDto, callbacks: DotNetObjectRef): boolean; // WebGL 불가면 false
export function focus(bodyId: string, immediate: boolean): void;
export function clearFocus(): void;
export function warp(bodyId: string): Promise<void>;  // 1초 이내에 resolve
export function dispose(): void;
```
C# 콜백: `[JSInvokable] OnBodyClicked(string id)`, `[JSInvokable] OnBodyHovered(string? id)`.
카메라 트윈 계산, 공전 위치 계산, URL↔포커스 변환 같은 순수 로직은 Three.js에 의존하지 않는 별도 모듈로 분리해 Vitest로 테스트한다.

## 횡단 관심사
- **에러 처리**:
  - 콘텐츠 fetch가 실패하면 HUD 스타일의 "SIGNAL LOST" 안내와 재시도 버튼을 보여 준다.
  - 없는 라우트나 slug는 테마형 404(LOST IN SPACE)로 보낸다. SWA 대체 경로 때문에 HTTP 상태는 200(soft 404)이다. 이는 알려진 한계다(ADR-001).
  - Blazor 기본 `#blazor-error-ui`는 테마에 맞게 스타일만 바꾼다.
- **설정**:
  - 사이트 설정은 `content/site.yml`, 천체는 `content/universe.yml`에 둔다. 코드에 하드코딩하지 않는다.
  - 빌드 설정은 MSBuild 속성으로 둔다(`Configuration`에 따라 초안 포함 여부 결정).
- **SEO**:
  - 페이지별 `<PageTitle>`, `HeadContent`의 description과 canonical은 런타임에 설정한다.
  - `index.html`에는 사이트 공통 OG 태그와 기본 OG 이미지를 둔다.
- **캐시**: `staticwebapp.config.json`에서 `/content/*`에 `Cache-Control: no-cache`를 건다(ETag로 재검증). 배포 직후 옛 콘텐츠가 보이지 않게 하기 위해서다.
- **SPA 라우팅**: `staticwebapp.config.json`의 `navigationFallback`을 `/index.html`로 한다. `/_framework/*`, `/content/*`, `/js/*`, `/css/*`, `/fonts/*`와 파일 확장자가 있는 경로는 제외해서, 없는 파일은 진짜 404가 나게 한다.
- **크기**: Web csproj에 `InvariantGlobalization=true`를 켠다. 날짜는 `yyyy-MM-dd` 형식 문자열로 직접 포맷한다. 한국어 표시에는 영향이 없다.
- **폰트**: woff2로 `wwwroot/fonts/`에 자체 호스팅한다(Michroma, Rajdhani, Pretendard dynamic subset). 외부 폰트 CDN에 의존하지 않는다.
- **로깅·관측**: MVP에는 없다(분석 도구 범위 밖).

## 테스트
| 대상 | 도구 | 명령 |
|---|---|---|
| ContentBuilder (front matter 파싱, 검증 실패, 인덱스·RSS·sitemap·TOC 생성, 초안 제외) | xUnit | `dotnet test` |
| Web 컴포넌트 (HUD 스탯 표시, 잠긴 행성, 상단 바, 목록·상세 렌더, 404) | bUnit + 가짜 `IContentService` | `dotnet test` |
| 씬 순수 로직 (트윈, 공전, URL↔포커스) | Vitest | `npm test --prefix scene` |
| 3D 렌더링 결과, 연출 | 수동 확인(sandbox, 실제 사이트) | - |

## CI/CD (`.github/workflows/`)
- **PR**: `dotnet test`, `npm test --prefix scene`
- **main 푸시**:
  1. 테스트를 실행한다.
  2. `dotnet publish src/Loonaciaverse.Web -c Release`로 게시한다.
  3. 전송량 예산(PRD 제약)을 검사한다.
  4. `Azure/static-web-apps-deploy`로 배포한다(`skip_app_build: true`, 게시 결과의 `wwwroot`를 업로드).
- SWA PR 미리보기 환경은 쓰지 않는다.
