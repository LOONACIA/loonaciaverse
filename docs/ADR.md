# Architecture Decision Records

> 되돌리기 어려운 기술 결정만 기록합니다. 스텝 description에서 "이 결정을 벗어나지 마라"의 근거로 쓰입니다.

## 원칙
- 작동하는 최소 구현을 우선한다. 외부 의존성은 대안을 검토한 뒤에만 추가한다.
- 서버와 DB 없이 정적 파일만으로 운영한다. 비용은 무료 플랜과 도메인뿐이다.
- 나중에 서버로 옮길 수 있는 길(ADR-001)을 막는 구조를 만들지 않는다. 그렇다고 지금 쓰지 않을 추상화를 미리 만들지도 않는다.
- 선택 상태의 유일한 원천은 URL이다. JS 씬은 상태 없는 렌더러로 둔다.

---

### ADR-001: Blazor WebAssembly Standalone(.NET 10)으로 시작한다
- **상태**: 채택
- **결정**: ASP.NET Core 서버 없이 .NET 10 Blazor WebAssembly Standalone 앱을 정적 파일로 게시한다. 나중에 BlazorWasmPreRendering.Build(빌드 시 프리렌더링)를 붙이거나, 호스팅 요금제에 따라 Blazor Web App(서버)으로 옮긴다.
- **이유**: 당장 구현하는 것이 목표다. 정적 호스팅은 무료다. 개발자가 익숙한 C#으로 클라이언트까지 작성하며 Blazor를 배울 수 있다.
- **트레이드오프**:
  - 서버 렌더링이 없어 글별 OG 태그가 없고, 공유 미리보기는 사이트 공통으로 나온다.
  - 첫 방문 때 .NET 런타임 다운로드 비용이 든다.
  - 없는 경로가 soft 404(HTTP 200)가 된다.
  - 검색 노출은 크롤러의 JS 렌더링과 sitemap에 의존한다.
  - 블로그 유입은 이관 전까지 후순위로 둔다.
- **하네스에 미치는 영향**:
  - 서버 프로젝트(ASP.NET Core 호스트), API, DB를 추가하지 마라.
  - Web 프로젝트 SDK는 `Microsoft.NET.Sdk.BlazorWebAssembly`를 유지한다.
  - 서비스 등록은 `Program.ConfigureServices` 정적 메서드에 모은다(ARCHITECTURE "레이어 규칙").

### ADR-002: 콘텐츠는 Markdown이고, 빌드할 때 변환한다
- **상태**: 채택
- **결정**: 글, 프로젝트, About은 `content/`의 Markdown(YAML front matter)으로 쓴다. 빌드할 때 .NET 콘솔 도구(ContentBuilder, Markdig + YamlDotNet)가 JSON·HTML, RSS, sitemap으로 바꾼다. 브라우저는 결과 JSON만 fetch한다. 발행은 main 푸시다. 초안은 `draft: true`로 표시하고 Release 빌드에서 뺀다.
- **이유**:
  - 목록, 태그, HUD 스탯용 인덱스는 어차피 빌드할 때 만들어야 한다.
  - Markdown 파서를 WASM 번들에서 빼서 가볍게 한다.
  - 나중에 서버로 옮겨도 같은 빌드 결과를 그대로 읽으면 된다.
  - git이 곧 백업이자 이력이다.
- **트레이드오프**:
  - 웹 에디터가 없다.
  - 글을 고치면 다시 빌드하고 배포해야 한다.
  - 저장소를 공개하면 커밋된 초안이 드러난다(공개 전에 다시 결정한다).
- **하네스에 미치는 영향**:
  - Web(WASM)에 Markdig, YamlDotNet 같은 파서 의존성을 추가하지 마라.
  - 런타임에 Markdown을 파싱하지 마라.
  - Headless CMS나 외부 콘텐츠 API를 도입하지 마라.

### ADR-003: 3D 씬은 Three.js + TypeScript + Vite로 만들고, HUD는 Blazor로 만든다
- **상태**: 채택
- **결정**:
  - 메인 화면 3D는 Three.js로 만들고 `scene/` npm 프로젝트(TypeScript, Vite 라이브러리 모드, Vitest)에서 빌드한다.
  - HUD, 내비게이션, 텍스트는 모두 Blazor 컴포넌트(HTML/CSS)이고 캔버스 위에 겹친다.
  - 예외는 3D 위치를 매 프레임 따라가야 하는 천체 이름표뿐이다. 씬 모듈이 Three.js CSS2DRenderer(HTML 라벨)로 관리한다.
  - 씬은 홈 페이지가 생길 때 만들고, 떠날 때 dispose한다.
  - C#과 JS 사이에는 ARCHITECTURE의 "씬 모듈 계약"만 쓴다.
- **이유**:
  - 천체 4개, 카메라 트윈, 후처리 정도의 씬에는 Three.js의 생태계와 크기가 가장 맞다.
  - TypeScript는 C# 개발자에게 익숙한 타입 안정성을 준다.
  - Vite 개발 서버로 Blazor 없이 셰이더와 카메라를 빠르게 다듬을 수 있다.
  - HUD가 HTML이면 텍스트가 선명하고 접근성과 스타일링이 쉽다.
- **트레이드오프**:
  - 툴체인이 .NET과 Node 두 개가 된다.
  - 3D 부분은 C#이 아니다.
  - 홈으로 돌아올 때마다 씬을 다시 초기화한다.
  - 레이아웃에 씬을 상주시키는 연출(역방향 워프 등)은 포기한다.
- **하네스에 미치는 영향**:
  - Babylon.js, Unity WebGL, Blazor용 Three.js 래퍼를 도입하지 마라.
  - HUD를 캔버스 안(Three.js 텍스트나 GUI)에 그리지 마라.
  - 씬이 선택 상태를 소유하거나 직접 라우팅하지 마라.
  - 씬을 MainLayout에 두지 마라.

### ADR-004: 프로젝트를 Core / Web / ContentBuilder로 나누고 IContentService를 경계로 둔다
- **상태**: 채택
- **결정**:
  - 의존성 없는 `Loonaciaverse.Core`에 콘텐츠 모델(record)과 `IContentService`를 둔다.
  - Web과 ContentBuilder가 둘 다 Core를 참조한다.
  - Web의 구현체는 `HttpContentService`(정적 JSON fetch) 하나다.
- **이유**:
  - 빌더가 쓰는 JSON과 웹이 읽는 JSON의 타입이 하나라 어긋나지 않는다.
  - 프리렌더링이나 Web App으로 옮길 때 서버용 구현체(파일 읽기)만 추가하면 된다.
- **트레이드오프**: 초반부터 프로젝트가 세 개다(테스트 제외).
- **하네스에 미치는 영향**:
  - 페이지와 컴포넌트에서 `HttpClient`로 콘텐츠를 직접 요청하지 마라.
  - 모델을 Web이나 ContentBuilder에 중복 정의하지 마라.
  - Core에 NuGet 의존성을 추가하지 마라.
  - Razor Class Library 분리는 하지 않는다(필요해지면 새 ADR).

### ADR-005: 콘텐츠 변환과 JS 번들을 dotnet build에 연결한다
- **상태**: 채택
- **결정**:
  - Web csproj의 MSBuild 타깃이 빌드 전에 ContentBuilder와 `scene/`의 `npm run build`를 실행한다(증분 빌드).
  - `dotnet watch`가 `content/`와 `scene/src/`의 변경을 감지한다.
- **이유**:
  - `dotnet build`나 `dotnet watch` 하나로 전체가 최신 상태가 된다.
  - 하네스 인수 조건과 CI가 단순해진다.
- **트레이드오프**:
  - 빌드에 Node가 필요하다.
  - 첫 빌드가 느리다(`npm ci`).
  - MSBuild 타깃을 관리해야 한다.
- **하네스에 미치는 영향**:
  - 빌드 단계를 별도 스크립트(`build.ps1` 등)로 쪼개지 마라.
  - 인수 조건은 `dotnet build`, `dotnet test`, `npm test --prefix scene`을 기본으로 쓴다.

### ADR-006: Azure Static Web Apps(Free)에 호스팅하고 main 푸시로 배포한다
- **상태**: 채택
- **결정**:
  - GitHub Actions에서 테스트하고 게시한 뒤 `Azure/static-web-apps-deploy`로 업로드한다(`skip_app_build: true`).
  - 라우팅, 헤더, 캐시는 `wwwroot/staticwebapp.config.json`에서 설정한다.
  - 보유한 루트 도메인을 연결하고 www는 루트로 리다이렉트한다.
  - PR 미리보기 환경은 쓰지 않는다.
- **이유**:
  - .NET 생태계와 자연스럽고, SPA 대체 경로와 헤더를 설정 파일 하나로 지정한다.
  - 나중에 C# Azure Functions를 붙이거나 App Service(Web App)로 옮겨도 같은 생태계에 머문다.
- **트레이드오프**:
  - 무료 플랜 제약이 있다(월 대역폭 100GB, 앱 크기 250MB).
  - 루트 도메인을 연결하려면 DNS가 ALIAS/ANAME이나 CNAME flattening을 지원해야 한다. Azure DNS는 유료라서 네임서버를 Cloudflare(Free)로 옮겨 DNS만 맡긴다(프록시 없이 DNS only).
  - Playground(Unity WebGL)를 도입할 때는 빌드 크기와 압축 파일(`.br`)의 `Content-Encoding` 헤더 처리 때문에 호스팅을 다시 검토해야 한다.
- **하네스에 미치는 영향**:
  - 다른 호스팅용 설정(GitHub Pages의 `404.html` 우회, Cloudflare `_headers` 등)을 추가하지 마라.
  - 배포 워크플로는 main 푸시 시에만 배포한다.

### ADR-007: 행성 비주얼은 절차적 셰이더로 만든다
- **상태**: 채택
- **결정**:
  - 천체의 표면, 대기, 글로우는 노이즈 기반 GLSL 셰이더로 만든다.
  - 천체마다 다른 것은 `universe.yml`의 `visual` 파라미터뿐이다.
  - 홀로그램 링 같은 HUD 요소도 셰이더나 라인 지오메트리로 그린다.
- **이유**:
  - 받을 텍스처가 없어 성능 예산(전송량 3MB)을 지키기 쉽다.
  - 실존 행성처럼 보이지 않아 "나만의 항성계"라는 컨셉에 맞는다.
- **트레이드오프**:
  - 셰이더 작성 난이도가 높다.
  - 텍스처 기반의 사실감은 포기한다.
- **하네스에 미치는 영향**:
  - 행성 텍스처 이미지, glTF 모델, HDR 환경맵을 추가하지 마라.
  - 새 천체의 모습은 셰이더 파라미터로만 바꾼다.
