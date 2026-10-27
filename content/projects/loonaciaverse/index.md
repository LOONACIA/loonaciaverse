---
title: LOONACIAVERSE
slug: loonaciaverse
summary: Blazor WebAssembly와 Three.js로 만든, 항성계를 탐험하듯 둘러보는 개인 사이트
period: 2026 - 현재
role: 기획 및 개발
stack: [csharp, blazor, threejs, typescript]
links:
  github: https://github.com/loonacia/loonaciaverse
featured: true
order: 1
draft: false
---
## 개요

프로젝트와 기술 기록을 항성계라는 테마로 소개하는 개인 사이트

## 개발 방식

계획과 리뷰는 Claude Code가, 구현은 Codex가 맡았다. 하네스 스크립트를 이용해 오케스트레이션 했다. 클로드가 작업을 스텝으로 나눈 계획을 쓰면, 스크립트가 스텝마다 코덱스를 새 세션으로 실행하고 인수 조건(대부분 테스트)을 확인한다. 커밋은 클로드가 diff를 리뷰해 통과시킨 뒤에만 한다. 리뷰에서 떨어지면 피드백을 달아 다시 시키고, 한 스텝에 세 번까지 시도한다. 코덱스는 커밋이나 브랜치 전환 등을 하지 못하게 했고, 정해진 범위를 벗어나는 파일을 수정할 때는 이유를 남기도록 제한했다.
