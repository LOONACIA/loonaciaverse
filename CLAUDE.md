# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트

우주(항성계) 컨셉의 개인 포트폴리오 겸 기술 블로그입니다. 서버 없는 정적 사이트로, .NET 10 Blazor WebAssembly Standalone 앱과 Three.js 씬을 Azure Static Web Apps에 배포합니다.

설계 문서가 기준입니다. 작업 전에 관련 절을 읽으세요.
- `docs/PRD.md`: 기능별 완료 기준, 범위 밖, 제약(홈 전송량 3MB 등)
- `docs/UX.md`: 화면·연출·HUD 카피·디자인 토큰
- `docs/ARCHITECTURE.md`: 디렉토리, 레이어 규칙, 씬 모듈 계약, 빌드·런타임 흐름, 테스트 범위
- `docs/ADR.md`: 되돌리기 어려운 결정. 각 ADR의 "하네스에 미치는 영향"은 금지 목록입니다(서버·DB 추가, WASM에 Markdown 파서 추가, 행성 텍스처·glTF 추가, 별도 빌드 스크립트 등).

코드가 `ARCHITECTURE.md`와 달라지면 문서를 고칩니다. 이때 구현한 부분만 반영하고, 아직 구현하지 않은 설계(프리렌더링 도입 등)는 지우지 않습니다.

## 명령

모두 저장소 루트에서 실행합니다. 빌드에 Node.js가 필요합니다(아래 "빌드 파이프라인").

```sh
dotnet build                      # 전체 빌드 (콘텐츠 변환과 scene 번들 포함)
dotnet test                       # .NET 테스트 전체
npm test --prefix scene           # Vitest 전체
npm run build --prefix scene      # tsc 타입 검사 + Vite 번들. Vitest는 타입 검사를 하지 않음 (dotnet build가 자동 실행)

dotnet watch --project src/Loonaciaverse.Web   # 개발 서버 (http://localhost:5064), content/·scene/src/ 변경 감지
npm run dev --prefix scene                     # Blazor 없이 씬만 확인하는 Vite sandbox
```

단일 테스트:
```sh
dotnet test tests/Loonaciaverse.Web.Tests --filter "FullyQualifiedName~HomeTests"
npm test --prefix scene -- orbit   # 파일 이름 필터
```

CI의 배포 전 검사(홈 전송량 예산)를 로컬에서 재현할 때는 기존 `artifacts/publish`를 먼저 지웁니다. 이전 publish의 지문 붙은 파일이 남으면 합계가 부풉니다.
```sh
dotnet publish src/Loonaciaverse.Web -c Release -o artifacts/publish
dotnet run --project tools/Loonaciaverse.BudgetCheck -- artifacts/publish/wwwroot
```

CI(`.github/workflows/ci.yml`)는 PR에서 테스트만 실행하고, main 푸시에서는 테스트 → Release publish → 예산 검사 → Azure SWA 배포까지 실행합니다. main에 푸시하면 곧바로 공개됩니다.

## 빌드 파이프라인

`src/Loonaciaverse.Web/Loonaciaverse.Web.csproj`의 MSBuild 타깃이 Blazor 컴파일 전에 두 가지를 증분으로 실행합니다(ADR-005).
1. **ContentBuilder**: `content/`의 Markdown·YAML을 `wwwroot/content/*.json`, `feed.xml`, `sitemap.xml`, `robots.txt`로 변환합니다. Debug는 초안을 포함하고 Release는 뺍니다. front matter 오류나 slug 중복은 빌드를 실패시킵니다.
2. **scene 번들**: `scene/`에서 `npm run build`를 실행해 `wwwroot/js/`(`scene.js`, `highlight.js`, `toc.js`)를 만듭니다. `scene/node_modules`가 없으면 `npm ci`를 먼저 실행합니다.

`wwwroot/content/`, `wwwroot/js/`, `feed.xml`, `sitemap.xml`, `robots.txt`는 생성물이며 gitignore 대상입니다. 직접 고치지 말고 `content/`, `scene/src/`, ContentBuilder를 고칩니다.

## 구조와 경계

- **Core** (`src/Loonaciaverse.Core`): 콘텐츠 모델(record)과 `IContentService`. 의존성이 없습니다. ContentBuilder가 쓰는 JSON과 Web이 읽는 JSON이 같은 타입이므로 모델을 다른 프로젝트에 중복 정의하지 않습니다. 직렬화 옵션은 `ContentJson.Options`.
- **ContentBuilder** (`tools/`): Markdig·YamlDotNet은 여기에만 둡니다(WASM 번들 크기).
- **Web** (`src/Loonaciaverse.Web`): 페이지·컴포넌트는 콘텐츠를 `IContentService`로만 읽습니다(`HttpClient`로 `content/`를 직접 요청하지 않음). 서비스 등록은 `Program.ConfigureServices` 정적 메서드 한 곳에 모읍니다.
- **scene** (`scene/`): Three.js 코드는 여기에만 있습니다. C#에서는 `Services/SceneInterop.cs`만 `./js/scene.js`를 import합니다. 공개 함수는 `ARCHITECTURE.md`의 "씬 모듈 계약"을 따릅니다. Three.js에 의존하지 않는 로직(`orbit`, `camera`, `warp`, `quality` 등)은 순수 함수로 분리해 테스트합니다.
- **선택 상태**: 어떤 천체가 선택됐는지는 URL `?focus=`가 유일한 원천입니다. `Components/StarSystem.razor`가 URL을 해석해 씬에 명령하고, 씬은 클릭만 `OnBodyClicked`로 알립니다. 씬이 라우팅하거나 선택 상태를 소유하지 않습니다. HUD는 Blazor가 그리고, 천체 이름표만 씬이 CSS2DRenderer로 그립니다.
- **천체 설정**: `content/universe.yml`(천체, `visual` 셰이더 파라미터, 공전)과 `content/site.yml`(제목, `baseUrl`)에 둡니다. 코드에 하드코딩하지 않습니다. 예외로 `wwwroot/index.html`의 공통 OG 태그는 `site.yml`과 같은 값을 직접 쓰며, `OpenGraphTests`가 일치를 검사합니다. `site.yml`을 바꾸면 `index.html`도 함께 고칩니다.
- `InvariantGlobalization=true`이므로 날짜는 `yyyy-MM-dd` 문자열로 직접 포맷합니다.

## 테스트 규칙

- 테스트를 `content/`의 실제 글·프로젝트(특정 slug, 개수, 값)에 묶지 않습니다. 변환 동작은 ContentBuilder 테스트처럼 `TemporaryDirectory`와 `ContentTestData`로 만든 픽스처로 검증합니다. 실제 빌드 결과를 읽는 테스트(`ContentBuildIntegrationTests`, `OpenGraphTests`)는 파일 생성·모델 역직렬화와 두 실제 소스 사이의 일치만 확인합니다.
- Web 테스트는 bUnit입니다. `FakeContentService`와 `TestContent`로 콘텐츠를 주고, 씬은 `context.JSInterop.SetupModule("./js/scene.js")`로 모의합니다.
- scene 테스트는 Vitest + happy-dom이며 렌더러·composer는 모의 객체로 대체합니다. 셰이더 표현과 연출은 sandbox에서 수동으로 확인합니다.

## 관례

- 코드 주석과 식별자는 영어로 씁니다. 지시가 한국어여도 같습니다.