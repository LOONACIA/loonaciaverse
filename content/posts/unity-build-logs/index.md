---
title: "[Unity] 빌드에서 로그 확인하기"
slug: unity-build-logs
date: 2024-01-04
summary: "Application.logMessageReceived로 유니티 빌드에서도 로그와 예외 메시지를 확인하는 방법."
tags: [unity, csharp]
draft: false
---

게임을 개발하다 보면 각종 기상천외한 오류들을 마주할 때가 종종 있기 마련이다. 대부분은 에디터에서 수정을 마친 후 빌드를 하겠지만, 그렇게 했음에도 실제 빌드로 테스트를 할 때는 또 다른 버그를 마주하곤 한다.

나는 빌드 후 테스트를 하다 버그를 발견할 때면 예외 메시지를 확인해 보고 싶다는 생각이 들 때가 많았다. 예외 메시지를 살펴보는 것만으로도 쉽게 해결의 실마리를 잡을 수 있는 경우가 대부분이기 때문이다. ~~물론 로그에도 찍히지 않는 버그라면 그 여파는...~~

유니티 에디터에서는 콘솔(단축키 Ctrl + Shift + C)을 통해 개발자가 찍는 로그나 각종 예외 메시지를 확인할 수 있는데, 이를 빌드 테스트에서도 활용할 수 있다면 좋을 것이다.

내가 찾은 간단한 방법은, 유니티에서 제공하는 로그 이벤트를 활용하는 것이다.

## Application.logMessageReceived

[공식 문서](https://docs.unity3d.com/ScriptReference/Application-logMessageReceived.html)

Application 클래스의 logMessageReceived 이벤트는 해당 클래스에 로그 메시지가 수신될 때 발생한다. 간단히 생각해서, 예외가 발생하거나 Debug.Log 등으로 로깅을 할 때 발생한다고 봐도 무방하다.

```csharp
void OnLogMessageReceived(string logString, string stackTrace, LogType type)
```

위는 logMessageReceived 이벤트 핸들러의 시그니처이다. 첫 번째 인자에는 로그 메시지가, 두 번째 인자에는 스택 트레이스 정보가, 세 번째 인자에는 해당 로그의 유형([#참고](https://docs.unity3d.com/ScriptReference/LogType.html))이 전달된다.

이를 활용하면 로그를 파일로 저장하거나, 인게임 화면에 출력해 실시간으로 확인하는 등 테스트에 도움을 받을 수 있다. 예를 들어 로그를 파일로 저장하고 싶다면, 이벤트 핸들러를 다음과 같이 구현해 볼 수 있을 것이다.

```csharp
private string _logFilePath;

private static void InitializeLogPath()
{
    // 에디터에서는 Assets 폴더를, 빌드에서는 Data 폴더를 저장 경로로 지정
    string directory = Path.Join(Application.dataPath, "Logs");
    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    _logFilePath = Path.Join(directory, string.Format("log-{0}.txt", DateTime.Now.ToString("yyMMdd_HHmmss")));
}

private void OnLogMessageReceived(string logString, string stacktrace, LogType type)
{
    string line = $"[{type}] {DateTime.Now:yyyy-MM-dd hh:mm:ss} {logString} {stacktrace}\n";
    File.AppendAllText(_logFilePath, line);
}
```

위 코드를 이용해 GameManager 등 싱글톤 클래스에서 수명에 맞게 적절한 이벤트 구독/해제 과정을 거친다면 빌드에서도 쉽게 로그를 확인할 수 있다.

## 결론

요즘 들어서 빌드 테스트의 중요성을 뼈저리게 느끼고 있다. 에디터에서 확인하고 "됐다 끝!"이 아니라, 꼭 빌드 후 빌드 파일로 플레이 테스트를 하자.
