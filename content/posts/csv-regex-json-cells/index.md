---
title: "[C#] Csv 셀 내부의 JSON string을 고려하는 정규표현식"
slug: csv-regex-json-cells
date: 2023-01-15
summary: "셀 안 JSON string의 쉼표는 건너뛰고 csv 행을 나누는 정규표현식을 정리했다."
tags: [csharp, regex]
draft: false
---

일반적으로 csv 파일은 쉼표를 기준으로 split하면 간단하게 파싱할 수 있지만, 셀 내부 데이터에 쉼표가 포함되어 있을 때(특히 JSON string인 경우 등) 별다른 예외 처리없이 split하면 셀 안에 있는 데이터까지 잘라버리는 등 원하지 않는 동작을 할 수 있다.

이럴 때는 csv 파싱 라이브러리를 쓴다든지 정규표현식을 통해 셀 내부에 있는 쉼표는 무시하고 파싱하는 과정이 필요하다.

다행히 csv로 저장되는 JSON string은 따옴표로 쉽게 구분되므로, 별다른 라이브러리를 사용할 필요 없이 구현이 간단한 편이다.

```csharp
string[] csv = File.ReadAllLines(@".\csv1.csv");
Regex regex = new(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
foreach (var line in csv)
{
	foreach (var cell in regex.Split(line))
	{
		Console.WriteLine(cell);
	}
}

// Raw Data
box,"{""x"":5,""y"":6, ""name"":""rect""}",5

// Output
box
"{""x"":5,""y"":6, ""name"":""rect""}"
5
```
